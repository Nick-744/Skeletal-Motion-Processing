import numpy as np



class LowPassFilter:
    def __init__(self):
        self.prev_raw:      float | None = None
        self.prev_filtered: float | None = None

        return;

    def process(self, value: np.ndarray, alpha: float) -> np.ndarray:
        if self.prev_raw is None:
            s = value
        else:
            s = alpha * value + (1.0 - alpha) * self.prev_filtered
        
        self.prev_raw      = value
        self.prev_filtered = s

        return s;



class OneEuroFilter:
    '''
    1€ filter: a simple speed-based low-pass filter for noisy input in interactive systems.
    Authors: Géry Casiez, Nicolas Roussel, Daniel Vogel
    '''

    def __init__(self,
            mincutoff: float =  1.0,
            beta:      float =  0.0,
            dcutoff:   float =  1.0,
            freq:      float = 30.0):
        self.freq      = freq
        self.mincutoff = mincutoff
        self.beta      = beta
        self.dcutoff   = dcutoff
        
        self.x_filter  = LowPassFilter()
        self.dx_filter = LowPassFilter()

        return;

    def _alpha(self, cutoff: float) -> float:
        ''' Compute smoothing factor for a given cutoff frequency. '''

        te  = 1.0 / self.freq            # Sampling period
        tau = 1.0 / (2 * np.pi * cutoff) # Time constant

        return 1.0 / (1.0 + tau / te);

    def process(self, x: np.ndarray) -> np.ndarray:
        ''' Compute the filtered signal. '''

        prev_x = self.x_filter.prev_raw

        dx  = 0.0 if prev_x is None else (x - prev_x) * self.freq   # Raw velocity
        edx = self.dx_filter.process(dx, self._alpha(self.dcutoff)) # Smoothed velocity

        cutoff = self.mincutoff + self.beta * np.abs(edx)

        return self.x_filter.process(x, self._alpha(cutoff));
