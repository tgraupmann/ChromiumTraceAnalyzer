using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ChromiumTraceAnalyzer
{
    class Program
    {
        class SourceInfo
        {
            public string src_file = string.Empty;
            public string src_func = string.Empty;
            public string dur = string.Empty;
            public string tdur = string.Empty;
            public string tts = string.Empty;
        }

        static Dictionary<string, int> m_nameOccurances = new Dictionary<string, int>();

        static List<string> m_nameOrder = new List<string>();

        static Dictionary<string, List<SourceInfo>> m_srcFiles = new Dictionary<string, List<SourceInfo>>();

        static List<string> m_srcOrder = new List<string>();

        static void Report()
        {
            Console.WriteLine();
            Console.WriteLine("Report Begin...");

            Console.WriteLine(".Counts");

            foreach (KeyValuePair<string, int> kvp in m_nameOccurances)
            {
                Console.WriteLine("FieldVal={0} Count={1}", kvp.Key, kvp.Value);
            }

            Console.WriteLine(".Name Order (newest last)");

            foreach (string val in m_nameOrder)
            {
                Console.WriteLine(val);
            }

            Console.WriteLine(".Source (newest last)");

            foreach (string srcFile in m_srcOrder)
            {
                if (m_srcFiles.ContainsKey(srcFile))
                {
                    foreach (SourceInfo info in m_srcFiles[srcFile])
                    {
                        int tts;
                        int.TryParse(info.tts, out tts);
                        float time = tts / (float)System.TimeSpan.TicksPerSecond;
                        Console.WriteLine("src={0} func={1} tts={2:F3} dur={3}", srcFile, info.src_func, time, info.dur);
                    }
                }
            }

            Console.WriteLine("...End of Report");
            Console.WriteLine();
        }

        static void FoundNewName(string fieldName, string fieldValue)
        {
            //Console.WriteLine("FieldName={0} FieldVal={1}", fieldName, fieldValue);
        }

        static void FoundNewSource(string fieldName, string fieldValue)
        {
            if (!m_srcFiles.ContainsKey(fieldValue))
            {
                m_srcFiles[fieldValue] = new List<SourceInfo>();
            }

            if (m_srcOrder.Contains(fieldValue))
            {
                m_srcOrder.Remove(fieldValue); //remove to add to end
            }
            m_srcOrder.Add(fieldValue);
        }

        static void FoundName(string fieldName, string fieldValue)
        {
            //Console.WriteLine("FieldName={0} FieldVal={1}", fieldName, fieldValue);

            if (m_nameOrder.Contains(fieldValue))
            {
                m_nameOrder.Remove(fieldValue); //remove to add to end
            }
            m_nameOrder.Add(fieldValue);

            if (m_nameOccurances.ContainsKey(fieldValue))
            {
                m_nameOccurances[fieldValue]++;
            }
            else
            {
                m_nameOccurances[fieldValue] = 1;
                FoundNewName(fieldName, fieldValue);
            }
        }

        static void FoundNewPair(string fieldName, string fieldValue, List<KeyValuePair<string, string>> group)
        {
            //Console.WriteLine("FieldName={0} FieldVal={1}", fieldName, fieldValue);
            group.Add(new KeyValuePair<string, string>(fieldName, fieldValue));
            switch (fieldName)
            {
                case "name":
                    FoundName(fieldName, fieldValue);
                    break;
                case "src_file":
                    FoundNewSource(fieldName, fieldValue);
                    break;
            }
        }

        static string GetVal(string fieldName, List<KeyValuePair<string, string>> group)
        {
            foreach (KeyValuePair<string, string> kvp in group)
            {
                if (kvp.Key.Equals(fieldName))
                {
                    return kvp.Value;
                }
            }
            return string.Empty;
        }

        static bool ContainsFunc(List<SourceInfo> items, string func)
        {
            foreach (SourceInfo info in items)
            {
                if (info.src_func.Equals(func))
                {
                    return true;
                }
            }
            return false;
        }

        static void RemoveFunc(List<SourceInfo> items, string func)
        {
            foreach (SourceInfo info in items)
            {
                if (info.src_func.Equals(func))
                {
                    items.Remove(info);
                    return;
                }
            }
        }

        static void FoundNewGroup(List<KeyValuePair<string, string>> group)
        {
            SourceInfo info = new SourceInfo();
            info.src_file = GetVal("src_file", group);
            info.src_func = GetVal("src_func", group);
            info.dur = GetVal("dur", group);
            info.tdur = GetVal("tdur", group);
            info.tts = GetVal("tts", group);

            if (!string.IsNullOrWhiteSpace(info.src_file))
            {
                if (!m_srcFiles.ContainsKey(info.src_file))
                {
                    m_srcFiles[info.src_file] = new List<SourceInfo>();
                }
                if (!string.IsNullOrEmpty(info.src_func))
                {
                    if (ContainsFunc(m_srcFiles[info.src_file], info.src_func))
                    {
                        RemoveFunc(m_srcFiles[info.src_file], info.src_func);
                    }
                    else
                    {
                        Console.WriteLine("src_file={0} src_func={1}", info.src_file, info.src_func);
                    }
                    if (!ContainsFunc(m_srcFiles[info.src_file], info.src_func))
                    {
                        m_srcFiles[info.src_file].Add(info);
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Begin Analysis...");

            using (FileStream fs = File.Open("../../trace.txt", FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    bool newBlock = false;
                    bool newQuote = false;
                    bool newField = false;
                    bool newValue = false;
                    string fieldName = string.Empty;
                    string fieldValue = string.Empty;
                    char[] buffer = new char[1];
                    List<KeyValuePair<string, string>> group = new List<KeyValuePair<string, string>>();
                    while (fs.Position < fs.Length)
                    {
                        sr.Read(buffer, 0, buffer.Length);
                        char c = buffer[0];
                        if (c > 0)
                        {
                            if (c == '{')
                            {
                                newBlock = true;
                                fieldName = "";
                                newField = true;
                                newValue = false;
                                group.Clear();
                            }
                            else if (c == '}')
                            {
                                FoundNewPair(fieldName, fieldValue, group);
                                FoundNewGroup(group);

                                newBlock = true;
                                fieldName = "";
                                newField = true;
                                newValue = false;
                            }
                            else if (char.IsWhiteSpace(c))
                            {
                                continue;
                            }
                            else if (newBlock &&
                                c == '"')
                            {
                                if (newQuote)
                                {
                                    newQuote = false;
                                }
                                else
                                {
                                    newQuote = true;
                                }
                                continue;
                            }
                            else if (char.IsDigit(c) ||
                                newQuote)
                            {
                                if (newField)
                                {
                                    fieldName += c.ToString();
                                }
                                if (newValue)
                                {
                                    fieldValue += c.ToString();
                                }
                            }
                            else if (newBlock &&
                                c == ':')
                            {
                                newField = false;
                                newValue = true;
                            }
                            else if (newBlock &&
                                c == ',')
                            {
                                if (newValue)
                                {
                                    FoundNewPair(fieldName, fieldValue, group);
                                }

                                newField = true;
                                newValue = false;
                                fieldName = string.Empty;
                                fieldValue = string.Empty;
                            }
                            else
                            {
                                newBlock = false;
                            }
                        }

                    }
                }
            }

            Report();

            Console.WriteLine();
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
