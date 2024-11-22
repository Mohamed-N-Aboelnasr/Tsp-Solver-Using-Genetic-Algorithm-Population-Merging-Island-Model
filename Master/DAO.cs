using System.Collections.Generic;
using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace Master
{
    public class DAO
    {
        public static void Write_csv(List<double> l,string path)
        {
            var csv = new List<string>();


            var line=string.Join( ",", l.ToArray() );
            var newLine = string.Format(line);
            csv.Add(newLine);    


            File.AppendAllLines(path, csv);
        }
        
        public static List<City> Read_csv(string path)
        {
            var cities = new List<City>();
            using (TextFieldParser csvParser = new TextFieldParser(path))
            {
                
                csvParser.CommentTokens = new string[] { "#" };
                csvParser.SetDelimiters(new string[] { "," });
                csvParser.HasFieldsEnclosedInQuotes = true;
                
                // Skip the row with the column names
                csvParser.ReadLine();
                
                while (!csvParser.EndOfData)
                {
                    // Read current line fields, pointer moves to the next line.
                    string[] fields = csvParser.ReadFields();
                    var city = new City() {Name = fields[0], x = double.Parse(fields[1]), y = double.Parse(fields[2])};
                    cities.Add(city);
                }
            }

            return cities;
        }
    }
}