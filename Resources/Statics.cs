using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vice.Resources
{
    public static class Statics
    {
        //public static readonly ObservableCollection<NoCom> NumberLookup = new ObservableCollection<NoCom>()
        //{
        //    new NoCom("one", 1)
        //};

        public static readonly ObservableCollection<string> NumList = new ObservableCollection<string>()
        {
            "zero", "one", "two", "three", "four", "five", "six",
            "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "forteen", "fifteen", "sixteen", "seventeen",
            "eighteen", "nineteen"
        };
    }

    //public class NoCom
    //{
    //    public NoCom(string numWord, int num)
    //    {
    //        Num = num;
    //        NumWord = numWord;
    //    }
    //
    //    public int Num { get; set; }
    //    public string NumWord { get; set; }
    //}
}
