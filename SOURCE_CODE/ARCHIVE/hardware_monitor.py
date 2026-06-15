import psutil
import time
import csv



# ---< Configuration >--- #
DURATION_SECONDS = 300
SAMPLE_INTERVAL  = 1.0
OUTPUT_FILE      = 'hardware_metrics.csv'

def find_processes() -> tuple:
    python_proc = None
    unity_proc  = None
    
    for p in psutil.process_iter(['name', 'cmdline']):
        try:
            cmdline = p.info['cmdline']
            name    = p.info['name']
            
            # Identify the Streamer Pipeline
            if cmdline and 'mano_unity_streamer.py' in ''.join(cmdline):
                python_proc = p
                
            # Identify Unity
            if name and ('my_hands_game' in name):
                unity_proc = p
                
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            continue;
            
    return (python_proc, unity_proc);

def main():
    print('Searching for MANO Streamer and Unity processes...')
    (python_proc, unity_proc) = find_processes()

    if not python_proc:
        print('Could not find mano_unity_streamer.py running...')
        return;
    if not unity_proc:
        print('Could not find Unity running...')
        return;

    print(f'Found Streamer (PID: {python_proc.pid}) and Unity (PID: {unity_proc.pid})')
    print(f'Recording metrics for {DURATION_SECONDS / 60} minutes...\n')

    # CSV for logging
    with open(OUTPUT_FILE, mode = 'w', newline = '') as file:
        writer = csv.writer(file)
        writer.writerow(['Time_sec', 'Streamer_CPU_%', 'Streamer_RAM_MB', 'Unity_CPU_%', 'Unity_RAM_MB', 'Total_System_CPU_%'])

        start_time = time.time()
        
        # Prime the CPU percent calculation
        python_proc.cpu_percent()
        unity_proc.cpu_percent()
        psutil.cpu_percent()
        time.sleep(0.5)

        for i in range(int(DURATION_SECONDS / SAMPLE_INTERVAL)):
            current_time = round(time.time() - start_time, 1)
            
            try:
                # Divide by psutil.cpu_count() to get absolute system percentage
                py_cpu = round(python_proc.cpu_percent() / psutil.cpu_count(), 2)
                py_ram = round(python_proc.memory_info().rss / (1024 * 1024), 2) # Convert bytes to MB
                
                un_cpu = round(unity_proc.cpu_percent() / psutil.cpu_count(), 2)
                un_ram = round(unity_proc.memory_info().rss / (1024 * 1024), 2) # Convert bytes to MB
                
                sys_cpu = psutil.cpu_percent()

                writer.writerow([current_time, py_cpu, py_ram, un_cpu, un_ram, sys_cpu])
                
                if i % 10 == 0:
                    print(f'[{current_time}s] Streamer: {py_cpu}% CPU, {py_ram}MB | Unity: {un_cpu}% CPU, {un_ram}MB')

            except psutil.NoSuchProcess:
                print('\nOne of the processes was closed. Stopping monitor...')
                break;

            time.sleep(SAMPLE_INTERVAL)

    print(f'\nDone! Metrics saved to {OUTPUT_FILE}')

    return;



if __name__ == '__main__':
    main()
