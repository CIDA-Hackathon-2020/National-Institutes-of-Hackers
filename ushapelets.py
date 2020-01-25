import numpy as np

"""
Implements U-Shapelet algorithm for clustering timeseries using shapelets
and modified for frequency of shapelet rather than clustering potential

Algorithm originally defined in: 
J. Zakaria, A. Mueen and E. Keogh, "Clustering Time Series Using Unsupervised-Shapelets," 
    2012 IEEE 12th International Conference on Data Mining, Brussels, 2012, pp. 785-794.
    doi: 10.1109/ICDM.2012.26
"""

WINDOW_STEP = 10 # stide size for sliding window over sequence to pull out shapelets

def __z_normalize(ts):
    """
    Compute Z-Normalization of timeseries
    @param ts: timeseries to normalize
    @returns: normalized form of ts
    """
    if np.std(ts) == 0:
        return 0
    return (ts - np.mean(ts)) / np.std(ts)

def __distance(x, y):
    return np.sqrt(np.sum((x - __z_normalize(y))**2))


def __compute_distance_array(shapelet, data):
    """
    Compute distance of shapelet in other timeseries for single array
    @param shapelet: shapelet being considered
    @param data: array of single timeseries
    @returns: minimum distance of shapelet in timeseries
    """
    s_norm = __z_normalize(shapelet) # normalize shapelet to mean=0, sd=1
    s_len = len(shapelet)

    # loop comprehension to compute euclidean distance from each substring in timeseries to shapelet
    dists = [ 
        np.sqrt(np.sum((s_norm - __z_normalize(data[j:j + s_len]))**2)) 
        for i in range(data.shape[0]) 
        for j in range(0, len(data) - s_len + 1, WINDOW_STEP)
    ]
    dists = np.reshape(np.vstack(dists), (len(data), -1))
    dists = np.min(dists, axis=1) # save minimum distance for each row in dataset

    return dists / np.sqrt(s_len)


def __compute_distance(shapelet, data):
    """
    Compute distance of shapelet in other timeseries
    @param shapelet: shapelet being considered
    @param data: matrix of timeseries
    @returns: minimum distance of shapelet in each timeseries
    """
    s_norm = __z_normalize(shapelet) # normalize shapelet to mean=0, sd=1
    s_len = len(shapelet)

    # loop comprehension to compute euclidean distance from each substring in timeseries to shapelet
    dists = [ 
        np.sqrt(np.sum((s_norm - __z_normalize(data[i, j:j + s_len]))**2)) 
        for i in range(data.shape[0]) 
        for j in range(0, len(data[i,:]) - s_len + 1, WINDOW_STEP)
    ]
    dists = np.reshape(np.vstack(dists), (data.shape[0], -1))
    dists = np.min(dists, axis=1) # save minimum distance for each row in dataset

    return dists / np.sqrt(s_len)


def __compute_gap(shapelet, data, min_support):
    """
    Compute gap score for shaplet (how well it separates data)
    @param shapelet: shapelet being considered
    @param data: matrix of timeseries
    @param min_support: minimum number of times shapelet should occur in data
    @returns: gap score and distance threshold of shapelet
    """
    dists = __compute_distance(shapelet, data) # compute distance matrix
    dists_sorted = np.sort(dists) # sort for more efficient lookup
    
    max_gap = 0 # gap score of shapelet
    dt = 0 # min difference in distance between the timeseries with shapelet vs those without

    for l in range(0, len(dists)-1):
        d = (dists_sorted[l] + dists_sorted[l+1]) / 2 # potential distance between the timeseries with shapelet vs those without
        Da = dists_sorted[dists_sorted < d] # data with shapelet
        Db = dists_sorted[dists_sorted > d] # data without shapelet

        # avoid division by 0
        if len(Db) == 0:
            break
        if len(Da) == 0:
            continue

        r = len(Da) / len(Db) # shapelet's separating power
    
        # only use shapelet if it can separate more than min_support timeseries
        if min_support / data.shape[0] < r and r < 1 - min_support / data.shape[0]:
            # compute gap measure for this shapelet
            ma = np.mean(Da)
            mb = np.mean(Db)
            sa = np.std(Da)
            sb = np.std(Db)
            gap = mb - sb - (ma + sa)
            
            # save max gap score
            if max_gap < gap:
                max_gap = gap
                dt = d
    
    return max_gap, dt
        

def find_shapelets(data,
                   min_support=10,
                   min_shapelet_len=30, 
                   max_shapelet_len=120,
                   step_size=30 ):
    """
    find any shapelets having support greater than the minimum
    @param data: matrix of timeseries
    @param min_support: the minimum occurance of a shaplet to be considered 
    @param min_shapelet_len: minimum length of shaplets to consider
    @param max_shapelet_len: maximum length of shaplets to consider
    @param step_size: step size between min and max shapelet length
    @return: list of mined shapelets
    """
    S = [] # set of shapelets, initially empty
    index_next = 0
    ts = data[index_next, :] # a timeseries from the data
    while True:
        s_hat = [] # list of subsequences
        gap = [] # gap score of shapelet
        dt = [] # distances between those with and without shapelet
        idx = [] # index at start of window (for plotting later)

        # consider shapelets in this range of lengths
        for sl in range(min_shapelet_len, max_shapelet_len+1, step_size):
            # each subsequence in the timeseries
            for i in range(0, len(ts) - sl + 1, WINDOW_STEP):
                if (np.std(ts[i:i+sl]))**2 < 0.05:
                    # skip any shapelets of little/no movement
                    s_hat.append([])
                    gap.append(-1)
                    dt.append(-1)
                    idx.append(i)
                else:
                    s_hat.append(ts[i:i+sl]) # subsequence of length sl
                    gap_i, dt_i = __compute_gap(s_hat[-1].copy(), data, min_support)
                    gap.append(gap_i)
                    dt.append(dt_i)
                    idx.append(i)
            print(sl)

        index = np.argmax(gap) # find shapelet with max score
        
        # check if data can still be mined
        dists = __compute_distance(s_hat[index].copy(), data)
        Da = dists[dists < np.max(dt)]
        d = np.mean(Da) + np.std(Da)

         # append output with shapelet and shapelet metadata
        threshold = d
        S.append({"shapelet": s_hat[index], "timeseries_idx": index_next, "time_idx": idx[index], "threshold": threshold}) # add to set of shapelets
        print("Shapelet of length {} minutes found in timeseries {} at time {} minutes".format(len(S[-1]['shapelet']), index_next, idx[index]))
        
        # stop mining if there is not enough support for a new shapelet
        if len(Da) <= min_support:
            break

        # get next timeseries as the one least likely to contain previous shapelet
        index_next = np.argmax(dists)
        ts = data[index_next, :]
        # remove points that contain previous shapelet
        data = data[dists < d, :]

    return S


def contains_shapelet(ts, shapelet, threshold):
    """
    Check if shapelet occurs in timeseries after shapelet mining
    @param ts: timeseries
    @param shapelet: shapelet to test
    @param threshold: maximum distance to be considered an occurance
    @returns -1 if no shapelet or index of most likely occurance otherwise
    """
    dists = __compute_distance_array(shapelet, ts)
    # are any distances below threshold? if yes then return most likely (smallest)
    if any(dists < threshold):
        return np.argmin(dists) * WINDOW_STEP
    return -1