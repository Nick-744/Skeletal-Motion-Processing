import csv
import statistics
import os
import sys

script_dir    = os.path.dirname(os.path.abspath(__file__))
csv_file_path = os.path.join(script_dir, "Assets", "LatencyLog.csv")

def main():
    if not os.path.exists(csv_file_path):
        print(f"Error: Letency log file not found at '{csv_file_path}'.")
        print("Make sure you've run the Unity game and let it log the latency first!")
        sys.exit(1)

    latencies = []

    print(f"Reading latency data from {csv_file_path}...")
    try:
        with open(csv_file_path, mode='r') as file:
            csv_reader = csv.DictReader(file)
            for row in csv_reader:
                try:
                    # Adjust 'LatencyMS' if the CSV header changes
                    latency = float(row['LatencyMS'])
                    latencies.append(latency)
                except ValueError:
                    continue
                except KeyError as e:
                    print(f"Format error: The exact column 'LatencyMS' was not found. Expected headers: TimestampMS,LatencyMS")
                    sys.exit(1)
    except Exception as e:
        print(f"Error reading {csv_file_path}: {e}")
        sys.exit(1)

    count = len(latencies)
    if count == 0:
        print("No valid latency data found in the CSV file.")
        sys.exit(1)

    # Calculate metrics using built-in statistics library
    mean_lat = statistics.mean(latencies)
    median_lat = statistics.median(latencies)
    min_lat = min(latencies)
    max_lat = max(latencies)
    std_dev = statistics.stdev(latencies) if count > 1 else 0.0

    # Calculate percentiles (95th and 99th) - requires Python 3.8+
    if hasattr(statistics, 'quantiles'):
        try:
            # Divide into 100 intervals (percentiles)
            percentiles = statistics.quantiles(latencies, n=100)
            p95_lat = f"{percentiles[94]:.2f} ms"
            p99_lat = f"{percentiles[98]:.2f} ms"
        except ValueError:
            p95_lat = p99_lat = "N/A (Not enough data points)"
    else:
        p95_lat = p99_lat = "N/A (Requires Python 3.8+)"

    # Print results
    print("\n" + "=" * 40)
    print("       LATENCY METRICS REPORT       ")
    print("=" * 40)
    print(f"Total entries : {count}")
    print(f"Minimum       : {min_lat:.2f} ms")
    print(f"Maximum       : {max_lat:.2f} ms")
    print(f"Mean (Avg)    : {mean_lat:.2f} ms")
    print(f"Median        : {median_lat:.2f} ms")
    print(f"Std Deviation : {std_dev:.2f} ms")
    print(f"95th Pctile   : {p95_lat}")
    print(f"99th Pctile   : {p99_lat}")
    print("=" * 40)

if __name__ == "__main__":
    main()
