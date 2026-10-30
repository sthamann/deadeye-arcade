using System.Xml.Linq;

namespace Reaper.Core;

public record CodeIntegrityEvent(DateTimeOffset? Time,string File,string Process,string Policy,string Status)
{
    public static CodeIntegrityEvent? Parse(string xml,DateTimeOffset? time=null)
    {
        var document=XDocument.Parse(xml);
        if(document.Descendants().FirstOrDefault(e=>e.Name.LocalName=="EventID")?.Value!="3077")return null;
        string Key(string value)=>string.Concat(value.Where(c=>!char.IsWhiteSpace(c) && c!='_'));
        var fields=document.Descendants().Where(e=>e.Name.LocalName=="Data" && e.Attribute("Name") is not null)
            .GroupBy(e=>Key((string)e.Attribute("Name")!),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First().Value,StringComparer.OrdinalIgnoreCase);
        string Value(params string[] names)=>names.Select(n=>fields.GetValueOrDefault(Key(n))).FirstOrDefault(v=>!string.IsNullOrWhiteSpace(v))??"";
        string file=Value("FileName","FileNameBuffer");if(file.Length==0)return null;
        return new(time,file,Value("ProcessName","ProcessNameBuffer"),Value("PolicyGUID","PolicyID"),Value("Status"));
    }
}
