using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;


namespace Hackathon
{
    class Program
    {
        private static string _folderPath = @"C:\Users\helmis\Desktop\data";
        static void Main(string[] args)
        {
            //MergeFiles();
            //GenerateMinuteCounts();
            //GenerateMinuteColumns();
            //GenerateSleepColumn();
            AddOtherMetrics();
        }

        // computes the daily TST, WASO, SF, and steps
        private static void AddOtherMetrics()
        {
            var inputFile = "data_minute_sleep_window.csv";
            var outputFile = "data_minute_metrics.csv";
            var outputPath = $@"{_folderPath}\{outputFile}";

            var file = File.ReadAllLines($@"{_folderPath}\{inputFile}").ToList();
            var header = file[0];
            header = $"{header},cu_daily_TST,cu_dialy_WASO,cu_daily_SF,cu_daily_step\n";
            File.WriteAllText(outputPath, header);
            file.RemoveAt(0);

            // load user data for one day and then calculate the measures
            var currentId = "1".PadLeft(2, '0');

            var currentUserData = new List<string>();

            foreach (var line in file)
            {
                var id = line.Split(",")[0];
                if (id == currentId)
                    currentUserData.Add(line);

                else
                {
                    // calculate TST, SF, WASO, steps per day
                    var currentDate = currentUserData[0].Split(",")[1];
                    var dayData = new List<string>();
                    foreach (var userLine in currentUserData)
                    {
                        var date = userLine.Split(",")[1];
                        if (date == currentDate)
                            dayData.Add(userLine);

                        else
                        {
                            AddDailyDataToFile(dayData, outputPath);
                            dayData.Clear();
                            currentDate = date;
                            dayData.Add(userLine);
                        }
                    }
                    AddDailyDataToFile(dayData, outputPath);

                    //reset
                    currentUserData.Clear();
                    currentId = id;
                    currentUserData.Add(line);
                }
            }

            var currentDate1 = currentUserData[0].Split(",")[1];
            var dayData1 = new List<string>();
            foreach (var userLine in currentUserData)
            {
                var date = userLine.Split(",")[1];
                if (date == currentDate1)
                    dayData1.Add(userLine);

                else
                {
                    AddDailyDataToFile(dayData1, outputPath);
                    dayData1.Clear();
                    currentDate1 = date;
                    dayData1.Add(userLine);
                }

            }
            AddDailyDataToFile(dayData1, outputPath);
        }

        // adds the daily variables  calculated above to file
        private static void AddDailyDataToFile(List<string> dayData, string outputPath)
        {

            // id[0],day[1],time[2],minute_met[3],cu_steps[4],posture[5],day_of_week[6],weekend[7],pa[8],minute_sleep[9],sleep[10]
            var values = dayData[0].Split(",");
            var stepsBegin = int.Parse(values[4]);
            var minuteSleep = int.Parse(values[9]);
            //var overallSleep = int.Parse(values[10]);
            var tst = 0;
            var waso = 0;
            var sf = 0;

            var awake = minuteSleep == 0;

            using (var sw = new StreamWriter(outputPath, append: true))
            {
                foreach (var line in dayData)
                {
                    values = line.Split(",");
                    minuteSleep = int.Parse(values[9]);
                    var overallSleep = int.Parse(values[10]);

                    tst += overallSleep;
                    waso += (minuteSleep == 0 && overallSleep == 1) ? 1 : 0;
                    if (overallSleep == 1 && minuteSleep == 0)
                    {
                        if (awake == false)
                        {
                            sf++;
                            awake = true;
                        }
                    }

                    if (overallSleep == 1 && minuteSleep == 1)
                        awake = false;

                    var cuSteps = int.Parse(values[4]);
                    var steps = cuSteps - stepsBegin;

                    sw.WriteLine($"{line},{tst},{waso},{sf},{steps}");
                }
            }
        }

        // selects subject data day by day and sends it for sleep detection
        private static void GenerateSleepColumn()
        {
            var inputFile = "data_minute_variables.csv";
            var outputFile = "data_minute_sleep_window.csv";

            var file = File.ReadAllLines($@"{_folderPath}/{inputFile}");

            var currentId = "1".PadLeft(2, '0');

            var currentUserData = new List<string>();
            var header = $"{file[0]},sleep\n";

            File.WriteAllText($@"{_folderPath}/{outputFile}", header);

            foreach (var line in file.Skip(1))
            {
                var id = line.Split(",")[0];
                if (id == currentId)
                    currentUserData.Add(line);

                else
                {
                    // process previous user
                    DetermineSubjectSleep(currentUserData, outputFile);

                    //reset
                    currentUserData.Clear();
                    currentId = id;
                    currentUserData.Add(line);
                }
            }
            DetermineSubjectSleep(currentUserData, outputFile);
        }

        // sleep detection: if subject has been in the sleep position for more than 30 cumulative minutes (no more than 10 minutes awake gap) => sleep onset
        private static void DetermineSubjectSleep(List<string> currentUserData, string outputFile)
        {
            var asleepThresholhd = 30;
            var awakeThresholhd = 10;

            var sleepWindow = 0;
            var consecutiveWake = 0;
            var sleepWindows = new List<int>();
            var minuteStatuses = new List<string>();
            // id[0],day[1],time[2],minute_met[3],cumulative[4],posture[5],day_of_week[6],weekend[7],pa[8],asleep[9]
            for (var i = 0; i < currentUserData.Count; i++)
            {
                var minuteLine = currentUserData[i];
                var minuteSleepString = minuteLine.Split(",").Last();
                var minuteSleep = minuteSleepString == "1" ? 1 : 0;
                if (minuteSleep == 0)
                {
                    consecutiveWake++;
                    if (consecutiveWake >= awakeThresholhd)
                    {
                        sleepWindow = 0;
                        for (int j = 1; j <= 9; j++)
                        {
                            var index = i - j;
                            if (index < 0)
                                break;
                            sleepWindows[index] = 0;
                        }
                    }
                    sleepWindows.Add(sleepWindow);
                }

                else
                {
                    consecutiveWake = 0;
                    sleepWindow++;
                    sleepWindows.Add(sleepWindow);
                }

                minuteStatuses.Add(minuteSleepString);
            }

            var finalSleep = new List<string>();

            while (sleepWindows.Any())
            {
                var asleepStatus = sleepWindows.Last() >= asleepThresholhd;

                if (asleepStatus) // currently asleep
                {
                    while (sleepWindows.Last() > 1)
                    {
                        finalSleep.Insert(0, "1");
                        sleepWindows.RemoveAt(sleepWindows.Count - 1);
                    }
                }

                else
                {
                    finalSleep.Insert(0, "0"); // add awake
                    sleepWindows.RemoveAt(sleepWindows.Count - 1);
                }
            }

            using (var sw = new StreamWriter($@"{_folderPath}/{outputFile}", append: true))
            {
                for (var i = 0; i < currentUserData.Count; i++)
                    sw.WriteLine($"{currentUserData[i]},{finalSleep[i]}");
            }
        }


        // looking only at the current minute, decides if the subject is asleep (posture=0 and low PA)
        private static void GenerateMinuteColumns()
        {
            var inputFile = "data_mergerd_clean.csv";
            var outputFile = "data_minute_variables.csv";

            var file = File.ReadAllLines($@"{_folderPath}/{inputFile}");
            var header = $"{file[0]},pa,minute_sleep";
            using (var sw = new StreamWriter($@"{_folderPath}/{outputFile}"))
            {
                sw.WriteLine(header);
                foreach (var line in file.Skip(1))
                {
                    var values = line.Split(",");
                    //id[0]	day[1]	time[2]	minute_met[3]	cumulative[4]	posture[5]	day_of_week[6]	weekend[7]
                    var met = double.Parse(values[3]);
                    var posture = values[5].Trim();
                    var pa = "";
                    if (met < 3)
                        pa = "0";

                    else if (met < 6)
                        pa = "1";

                    else
                        pa = "2";

                    var asleep = pa == "0" && posture == "0" ? "1" : "0";
                    sw.WriteLine($"{line},{pa},{asleep}");
                }
            }
        }

        // deletes the days with mising data from dataset
        private static void DropIncompleteDays()
        {
            var inputFile = "data_mergerd_not-clean.csv";
            var outputFile = "data_mergerd_clean.csv";

            var dropList = GetIncompleteDays();

            var file = File.ReadAllLines($@"{_folderPath}/{inputFile}");

            using (var sw = new StreamWriter($@"{_folderPath}/{outputFile}"))
            {
                sw.WriteLine(file[0]); // add header
                foreach (var line in file.Skip(1))
                {
                    var values = line.Split(",");
                    var id = values[0];
                    var day = values[1];
                    if (!dropList.Contains($"{id}_{day}"))
                        sw.WriteLine(line);
                }
            }
        }

        // marks days with missing data to be deleted (usually only the first day and the last day)
        private static HashSet<string> GetIncompleteDays()
        {
            var dropList = new HashSet<string>();
            var inputPath = $@"{_folderPath}\minute_count.csv";

            var file = File.ReadAllLines(inputPath);
            // id => day => count full hours
            var counts = new Dictionary<string, Dictionary<string, int>>();

            foreach (var line in file)
            {
                var values = line.Split(",");
                var id = values[0];
                var day = values[1];
                var hour = values[2];
                var minutes = int.Parse(values[3]);

                if (minutes < 60)
                    continue;

                if (!counts.ContainsKey(id))
                    counts.Add(id, new Dictionary<string, int>());

                if (!counts[id].ContainsKey(day))
                    counts[id].Add(day, 0);

                counts[id][day]++;
            }

            foreach (var idDayHours in counts)
                foreach (var dayHours in idDayHours.Value)
                    if (dayHours.Value < 24)
                    {
                        Console.WriteLine($"{idDayHours.Key},{dayHours.Key}: {dayHours.Value}");
                        dropList.Add($"{idDayHours.Key}_{dayHours.Key}");
                    }
            return dropList;
        }

        // check how my rows per subject per date per hour exists. it will be used to find incomplete days
        private static void GenerateMinuteCounts()
        {
            var filePath = $@"{_folderPath}\data_mergerd_not-clean.csv";
            var file = File.ReadAllLines(filePath).Skip(1);
            var outputPath = $@"{_folderPath}\minute_count.csv";

            File.WriteAllText(outputPath, "");
            // id => day => hour => count
            var minuteCounts = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();

            var sb = new StringBuilder();
            foreach (var line in file)
            {
                var values = line.Split(",");
                var id = values[0];
                var day = values[1];
                var time = values[2].Split(" ").Last() + " " + values[2].Split(":").First().PadLeft(2, '0');

                //Console.WriteLine($"id: {id}");

                if (!minuteCounts.ContainsKey(id))
                    minuteCounts.Add(id, new Dictionary<string, Dictionary<string, int>>());

                if (!minuteCounts[id].ContainsKey(day))
                    minuteCounts[id].Add(day, new Dictionary<string, int>());

                if (!minuteCounts[id][day].ContainsKey(time))
                    minuteCounts[id][day].Add(time, 0);

                minuteCounts[id][day][time]++;
            }

            using (var sw = new StreamWriter(outputPath))
            {
                foreach (var idDayTimeCount in minuteCounts.OrderBy(x => x.Key))
                {
                    var idKey = idDayTimeCount.Key;
                    foreach (var dayTimeCount in idDayTimeCount.Value.OrderBy(x => x.Key))
                    {
                        var dayKey = dayTimeCount.Key;
                        foreach (var hourCount in dayTimeCount.Value.OrderBy(x => x.Key))
                        {
                            var hourKey = hourCount.Key;
                            sw.WriteLine($"{idKey},{dayKey},{hourKey},{hourCount.Value}");
                        }
                    }
                }
            }
        }

        // merges files, converts date to standard date, splits date and time, removes rows with missing date
        private static void MergeFiles()
        {
            var monthMap = new Dictionary<string, string> {
                { "JAN", "01" },
                { "FEB", "02" },
                { "MAR", "03" },
                { "APR", "04" },
                { "MAY", "05" },
                { "JUN", "06" },
                { "JUL", "07" },
                { "AUG", "08" },
                { "SEP", "09" },
            { "OCT", "10" },
            { "NOV", "11" },
            { "DEC", "12" }};

            var outputPath = $@"{_folderPath}\data_mergerd_not-clean.csv";
            var filePaths = Directory.GetFiles(_folderPath)
                .Where(x => Path.GetFileNameWithoutExtension(x)[0] == 'm');

            var header = "id,day,time,minute_met,cumulative,posture,day_of_week,weekend";
            using (var sw = new StreamWriter(outputPath))
            {
                sw.WriteLine(header);
                foreach (var filePath in filePaths)
                {
                    var id = Path.GetFileNameWithoutExtension(filePath)
                        .Split("_")[1];
                    var file = File
                        .ReadAllLines(filePath)
                        .Skip(1)
                        .ToList();

                    for (var i = 0; i < file.Count(); i++)
                    {
                        var line = file[i];
                        var values = line.Split(",");

                        if (string.IsNullOrWhiteSpace(values[0]))
                            continue;

                        var dateTimeString = values[0].Split(":");
                        if (dateTimeString.Count() < 4)
                            continue;
                        var day = dateTimeString[0].Substring(0, 2);
                        var monthString = dateTimeString[0].Substring(2, 3);
                        var year = dateTimeString[0].Substring(5, 2);

                        var hour = dateTimeString[1];
                        var minute = dateTimeString[2];

                        var s = $"{monthMap[monthString]}-{day}-{year} {hour}:{minute}";
                        var datetime = DateTime.ParseExact(s, "MM-dd-yy HH:mm", CultureInfo.InvariantCulture);

                        var isWeekend = (datetime.DayOfWeek == DayOfWeek.Saturday) ||
                            (datetime.DayOfWeek == DayOfWeek.Sunday) ? "1" : "0";

                        var mets = values[1];
                        var cumulativeSteps = values[2];
                        var posture = values[3];

                        var outputString = $"{id},{datetime.ToString("MM/dd/yyyy")},{datetime.ToShortTimeString()}," +
                                           $"{mets},{cumulativeSteps},{posture},{(int)datetime.DayOfWeek},{isWeekend}";

                        sw.WriteLine(outputString);
                    }
                }
            }
        }
    }
}