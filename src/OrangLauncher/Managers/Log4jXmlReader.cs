using System.Text;
using System.Text.RegularExpressions;
namespace OrangLauncher.Managers
{
    public class Log4jXmlReader
    {
        private StringBuilder? _event;
        private static readonly Regex Attr = new(@"(\w+)=""([^""]*)""", RegexOptions.Compiled);
        private static readonly Regex Message = new(@"<log4j:Message><!\[CDATA\[(.*?)\]\]></log4j:Message>", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex Throwable = new(@"<log4j:Throwable><!\[CDATA\[(.*?)\]\]></log4j:Throwable>", RegexOptions.Compiled | RegexOptions.Singleline);
        public IEnumerable<string> Feed(string line)
        {
            var trimmed = line.TrimStart();
            if (_event == null)
            {
                if (!trimmed.StartsWith("<log4j:Event"))
                {
                    yield return line;
                    yield break;
                }
                _event = new StringBuilder();
            }
            _event.AppendLine(line);
            if (!trimmed.Contains("</log4j:Event>")) yield break;
            var text = _event.ToString();
            _event = null;
            foreach (var l in Format(text)) yield return l;
        }
        private static IEnumerable<string> Format(string xml)
        {
            var head = xml[..Math.Max(0, xml.IndexOf('>'))];
            var attrs = Attr.Matches(head).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
            var time = "";
            if (attrs.TryGetValue("timestamp", out var ts) && long.TryParse(ts, out var ms))
                time = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("HH:mm:ss");
            var thread = attrs.GetValueOrDefault("thread", "main");
            var level = attrs.GetValueOrDefault("level", "INFO");
            var msg = Message.Match(xml);
            var body = msg.Success ? msg.Groups[1].Value : "";
            var lines = body.Replace("\r", "").Split('\n');
            yield return $"[{time}] [{thread}/{level}]: {lines[0]}";
            foreach (var extra in lines.Skip(1))
                if (extra.Length > 0) yield return extra;
            var thr = Throwable.Match(xml);
            if (thr.Success)
                foreach (var t in thr.Groups[1].Value.Replace("\r", "").Split('\n'))
                    if (t.Trim().Length > 0) yield return t;
        }
    }
}