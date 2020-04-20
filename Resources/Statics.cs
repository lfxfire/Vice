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

        public static readonly Dictionary<char, int> KeyLookup = new Dictionary<char, int>()
        {
            {'A', 0x41}, {'B', 0x42}, {'C', 0x43}, {'D', 0x44}, {'E', 0x45}, {'F', 0x46}, {'G', 0x47}, {'H', 0x48}, {'I', 0x49},
            {'J', 0x4A}, {'K', 0x4B}, {'L', 0x4C}, {'M', 0x4D}, {'N', 0x4E}, {'O', 0x4F}, {'P', 0x50}, {'Q', 0x51}, {'R', 0x52},
            {'S', 0x53}, {'T', 0x54}, {'U', 0x55}, {'V', 0x56}, {'W', 0x57}, {'X', 0x58}, {'Y', 0x59}, {'Z', 0x5A}, {' ', 0x20}
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
