"""
# call with python mark_shapelets.py
# requires file ushaplets.py
"""


"""
import libraries and data file
"""
import matplotlib.pyplot as plt
import pandas as pd
import numpy as np

from ushapelets import *

# set seed for reproducability
np.random.seed(1234)

"""
read in data and sample subjects to be used in shapelet mining
"""
data = pd.read_csv("data_minute_sleep_window.csv")
sample_subs = np.random.choice(pd.unique(data.id), 4, replace=False)
data_sub = data[data['id'].isin(sample_subs)]

"""
convert data frame into matrix of complete days using MET minutes
"""
day_mat = []
mapper = []
for i in sample_subs:
    # get a subject's complete measurement
    subject = data_sub[data_sub.id == i]
    # transform each complete day into a single row
    for day in pd.unique(subject.day):
        if subject[subject.day == day].shape[0] < 24*60:
            print("Incomplete Day. Skipping...")
            continue
        # add to matrix
        day_mat.append(subject[subject.day == day].minute_met)
        # keep track of which row maps to which subject/day
        mapper.append((i, day, len(subject[subject.day == day].minute_met)))
# convert list to matrix
day_mat = np.vstack(day_mat)

"""
find shapelets in data in unsupervised fashion
"""
# further sample data to save time
sample_inds = np.random.choice(day_mat.shape[0], 50, replace=False)
sample = day_mat[sample_inds,:]
S = find_shapelets(sample, 
                   min_support=5,
                   min_shapelet_len=120,
                   max_shapelet_len=240,
                   step_size=60)

"""
save indicator variables of shapelets for each minute in data
"""
# use copy in case something goes wrone
data1 = data_sub.copy()
# enumerate the shapelets (add a new binary variable for each one)
for k, s in enumerate(S):
    print("Looking for Shapelet {} in data".format(k+1))
    # create new column for shapelet, initially all 0
    data1["Shapelet_{}".format(k)] = pd.Series(np.zeros((data.shape[0],)))
    row_num = 0 # to keep track of row in matrix
    # remap rows from day-row matrix back to exanded minute-by-minute dataframe
    for i, d, n in mapper:
        for j in range(n):
            # check for existence of shapelet
            loc = contains_shapelet(day_mat[row_num,:], s['shapelet'], s['threshold'])
            row_num += 1
            # if shapelet exists, set index of first occurance to 1 (for whole length of shapelet)
            if (loc != -1):
                data1[data1.id == i][data1.day == d]["Shapelet_{}".format(k)].values[j:len(s['shapelet'])] = 1
# save file
data1.to_csv("data_minute_sleep_window_shapelets.csv")