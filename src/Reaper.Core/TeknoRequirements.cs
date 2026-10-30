using System.Xml.Linq;

namespace Reaper.Core;

// The user's current profile cannot prove that a whole required row is missing.
// Compare it with the separate template supplied by the installed emulator.
public record TeknoRequirements(string Template, ControlRow[] Rows)
{
    public static string TemplatePath(GameEntry game)=>Path.Combine(game.WorkingDirectory,"GameProfiles",Path.GetFileName(game.SourcePath));
    public static TeknoRequirements? Read(GameEntry game)
    {
        if(game.Source!="teknoparrot" || !File.Exists(game.SourcePath))return null;
        string template=TemplatePath(game);
        if(!File.Exists(template) || Path.GetFullPath(template).Equals(Path.GetFullPath(game.SourcePath),StringComparison.OrdinalIgnoreCase))return null;
        var current=XDocument.Load(game.SourcePath);var original=XDocument.Load(template);
        static string Value(XElement node,string name)=>node.Elements().FirstOrDefault(e=>e.Name.LocalName==name)?.Value??"";
        string profile=current.Root is {} root?Value(root,"EmulationProfile"):"";
        if(string.IsNullOrWhiteSpace(profile) || original.Root is null || Value(original.Root,"EmulationProfile")!=profile)return null;
        string? api=current.Descendants().Where(e=>e.Name.LocalName=="FieldInformation").FirstOrDefault(e=>Value(e,"FieldName")=="Input API") is {} field?Value(field,"FieldValue"):null;
        if(api is not ("RawInput" or "DirectInput" or "XInput"))return null;
        var rows=original.Descendants().Where(e=>e.Name.LocalName=="JoystickButtons" && e.Elements().Any(c=>c.Name.LocalName=="ButtonName"))
            .Where(e=>Value(e,"HideWith"+api)!="true" && GameControlAudit.GameplayAction(Value(e,"ButtonName")))
            .Select(e=>new ControlRow(OverlayControls.TeknoPlayer(Value(e,"InputMapping"),Value(e,"ButtonName")),Value(e,"ButtonName"),"","TeknoParrot vendor template",IsAim:OverlayControls.TeknoAim(e),SourceKey:Value(e,"InputMapping"))).ToArray();
        return rows.Any(r=>r.IsAim && r.Player is 1 or 2)?new(template,rows):null;
    }
    public ControlRow? Match(ControlRow required, IEnumerable<ControlRow> actual)
    {
        var candidates=actual.Where(r=>r.Player==required.Player).ToArray();
        var named=candidates.Where(r=>r.Function==required.Function).ToArray();
        if(named.Length==1)return string.IsNullOrWhiteSpace(required.SourceKey) || named[0].SourceKey==required.SourceKey ? named[0] : null;
        if(named.Length>1 || string.IsNullOrWhiteSpace(required.SourceKey))return null;
        var keyed=candidates.Where(r=>r.SourceKey==required.SourceKey).ToArray();
        return keyed.Length==1 && keyed[0].IsAim==required.IsAim?keyed[0]:null;
    }
    public int RestoreMissing(XDocument current)
    {
        if(current.Root is null || !File.Exists(Template))return 0;
        var container=current.Root.Elements().FirstOrDefault(e=>e.Name.LocalName=="JoystickButtons");
        if(container is null){container=new XElement("JoystickButtons");current.Root.Add(container);}
        static string Value(XElement node,string name)=>node.Elements().FirstOrDefault(e=>e.Name.LocalName==name)?.Value??"";
        ControlRow Row(XElement e)=>new(OverlayControls.TeknoPlayer(Value(e,"InputMapping"),Value(e,"ButtonName")),Value(e,"ButtonName"),"","",IsAim:OverlayControls.TeknoAim(e),SourceKey:Value(e,"InputMapping"));
        var original=XDocument.Load(Template).Descendants().Where(e=>e.Name.LocalName=="JoystickButtons" && e.Elements().Any(c=>c.Name.LocalName=="ButtonName")).ToArray();
        int changes=0;
        foreach(var required in Rows)
        {
            var actual=container.Elements().Where(e=>e.Name.LocalName=="JoystickButtons").Select(Row).ToArray();
            if(Match(required,actual) is not null)continue;
            var named=container.Elements().Where(e=>e.Name.LocalName=="JoystickButtons" && Row(e).Player==required.Player && Value(e,"ButtonName")==required.Function).ToArray();
            if(named.Length==1 && Row(named[0]).IsAim==required.IsAim && !string.IsNullOrWhiteSpace(required.SourceKey))
            {
                // As in the vendor's revision merge, the named action keeps its
                // settings while its routing identifier comes from the template.
                named[0].SetElementValue("InputMapping",required.SourceKey);changes++;continue;
            }
            // A conflicting row needs review; never duplicate the same named input.
            if(actual.Any(r=>r.Player==required.Player && (r.Function==required.Function || !string.IsNullOrWhiteSpace(required.SourceKey) && r.SourceKey==required.SourceKey)))continue;
            var candidates=original.Where(e=>Value(e,"ButtonName")==required.Function && Value(e,"InputMapping")==required.SourceKey).ToArray();
            if(candidates.Length!=1)continue;
            var restored=new XElement(candidates[0]);
            restored.Elements().Where(e=>e.Name.LocalName is "RawInputButton" or "BindName" or "BindNameRi" or "BindNameDi" or "BindNameXi").Remove();
            container.Add(restored);changes++;
        }
        return changes;
    }
}
