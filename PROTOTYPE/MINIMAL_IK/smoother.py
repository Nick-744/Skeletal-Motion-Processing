import numpy as np



class LowPassFilter:
    def __init__(self):
        self.prev_raw_value      = None
        self.prev_filtered_value = None

        return;

    def process(self, value: np.ndarray, alpha: float) -> np.ndarray:
        if self.prev_raw_value is None:
            s = value
        else:
            s = alpha * value + (1.0 - alpha) * self.prev_filtered_value
        
        self.prev_raw_value      = value
        self.prev_filtered_value = s

        return s;



class OneEuroFilter:
    '''
    1€ filter: a simple speed-based low-pass filter for noisy input in interactive systems.
    Authors: Géry Casiez, Nicolas Roussel, Daniel Vogel
    '''

    def __init__(self,
            mincutoff: float = 1.0,
            beta:      float = 0.0,
            dcutoff:   float = 1.0,
            freq:      float = 30.0):
        self.freq      = freq
        self.mincutoff = mincutoff
        self.beta      = beta
        self.dcutoff   = dcutoff
        self.x_filter  = LowPassFilter()
        self.dx_filter = LowPassFilter()

        return;

    def compute_alpha(self, cutoff):
        te  = 1.0 / self.freq
        tau = 1.0 / (2 * np.pi * cutoff)

        return 1.0 / (1.0 + tau / te);

    def process(self, x):
        prev_x = self.x_filter.prev_raw_value
        dx     = 0.0 if prev_x is None else (x - prev_x) * self.freq
        edx    = self.dx_filter.process(dx, self.compute_alpha(self.dcutoff))
        cutoff = self.mincutoff + self.beta * np.abs(edx)

        return self.x_filter.process(x, self.compute_alpha(cutoff));
