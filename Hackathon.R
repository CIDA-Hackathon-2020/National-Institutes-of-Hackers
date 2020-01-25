##### Hackathon

library(openxlsx)
library(data.table)
library(ggplot2)
library(gridExtra)
library(scales)
library(tsmp)
library(arulesSequences)
library(dplyr)
library(forecast)
library(reshape2)

library(vars)



#### Plotting Cleaned data
### Read in data and create plots

cleaned <- read.csv("./Desktop/hackathon/data_mergerd_clean.csv", stringsAsFactors = FALSE)
cleaned$time_new <- paste(cleaned$day, cleaned$time)
cleaned$time_new <- as.POSIXct(cleaned$time_new, format = "%m/%d/%Y %I:%M %p") # convert variables to time variables

jpeg(file = sprintf("./Desktop/hackathon/subject_1_cleaned.jpeg", i), units = 'in', height = 16 , width = 16, res = 100 )
p1 <- ggplot(cleaned[cleaned$id == 1,], aes(x = time_new, y = minute_met)) + geom_line() + scale_x_datetime(date_breaks = "days" , date_labels = "%b-%d")
p2 <- ggplot(cleaned[cleaned$id == 1,], aes(x = time_new, y = cumulative)) + geom_line()+ scale_x_datetime(date_breaks = "days" , date_labels = "%b-%d")
p3 <- ggplot(cleaned[cleaned$id == 1,], aes(x = time_new, y = posture)) + geom_line()+ scale_x_datetime(date_breaks = "days" , date_labels = "%b-%d")
grid.arrange(p1,p2,p3, nrow = 3)
dev.off()







##### Sequence mining

data <- read.csv("./Desktop/hackathon/data_minute_metrics.csv")


data$eventID <-  ave(data$id==data$id, data$id, FUN=cumsum) # create eventID for time of day for each subject


#transform data to be used 

dat <- data[, c("id", "eventID", "minute_met")]
dat <- dcast(dat, eventID ~ id) # transform
dat <- dat[complete.cases(dat),] # keep complete cases
dt <- dat[dat$eventID <= 1440,] # restrict to one day (computational efficiencty)
dat <- ts(dat) #make timeseries objects
 

numDiffs <- ndiffs(dat ) # calculate the optimal number of diffs to be used with forecast function
dat.diff <- diff(dat, differences = numDiffs) # Diff data


# fit the model 
dat.var <- VAR(dat.diff, lag.max=180) # fits a separate regression using lm of each time series on the lags (3 hours) of itself and the other series
# Basically trying to find autocorrelation based on lag

### Didn't work, still running. 


