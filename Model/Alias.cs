using System.Linq;

namespace ArkPlotWpf.Model
{
    public class StringDict : Dictionary<string, string>
    {
        public StringDict() { }

        private StringDict(Dictionary<string, string> dictionary) : base(dictionary) { }

        public static StringDict FromEnumerable(IEnumerable<KeyValuePair<string, string>> kvpList)
        {
            return new StringDict(kvpList.ToDictionary(pair => pair.Key, pair => pair.Value));
        }
    }
}

