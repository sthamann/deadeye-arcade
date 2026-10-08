using System.Xml.Linq;
namespace Reaper.Core;

public static class TeknoDisplaySetup
{
    public static bool Supports(GameEntry game) => game.Source=="teknoparrot"
        && Path.GetFileNameWithoutExtension(game.SourcePath) is "AliensExtermination" or "AliensArmageddon";
    public static bool Configure(GameEntry game,int width,int height)
    {
        if(!Supports(game) || width<640 || height<480) return false;
        var document=XDocument.Load(game.SourcePath); bool changed=false;
        void Set(string name,string value)
        {
            // Use existing vendor fields only, never invent unsupported renderer options.
            var field=document.Descendants("FieldInformation").SingleOrDefault(f=>f.Element("CategoryName")?.Value=="General" && f.Element("FieldName")?.Value==name)?.Element("FieldValue");
            if(field is null || field.Value==value) return;
            field.Value=value; changed=true;
        }
        // Composited borderless fullscreen allows the gun menu to remain visible above DX9/OpenGL games.
        Set("Windowed","1"); Set("CustomResolution","1");
        Set("ResolutionWidth",width.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Set("ResolutionHeight",height.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if(!changed) return false;
        string backup=game.SourcePath+".before-deadeye-display-fix";
        if(!File.Exists(backup)) File.Copy(game.SourcePath,backup);
        string temporary=game.SourcePath+".reaper-new";document.Save(temporary);File.Move(temporary,game.SourcePath,true);
        return true;
    }
}
