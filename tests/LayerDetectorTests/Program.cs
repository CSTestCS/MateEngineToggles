using System;
using System.Reflection;
using System.Text.RegularExpressions;
using VRoidClothingToggles;

// Only exercises string logic; no Unity runtime is needed for these methods.
static class Program
{
    static int failures;

    static void Check(string what, object got, object want)
    {
        bool ok = Equals(got, want);
        if (!ok) failures++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what} => {got}{(ok ? "" : $" (expected {want})")}");
    }

    static int Main()
    {
        var t = typeof(LayerDetector);
        var regex = (Regex)t.GetField("VRoidMaterial", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var isCloth = t.GetMethod("IsVRoidClothing", BindingFlags.NonPublic | BindingFlags.Static);

        bool IsLayer(string mat)
        {
            var m = regex.Match(LayerDetector.CleanName(mat));
            return m.Success && (bool)isCloth.Invoke(null, new object[] { m.Groups["part"].Value, m.Groups["kind"].Value });
        }
        string Key(string mat) => regex.Match(LayerDetector.CleanName(mat)).Value;

        // Typical VRoid Studio material names.
        Check("Tops is a layer", IsLayer("N00_002_01_Tops_01_CLOTH (Instance)"), true);
        Check("Bottoms is a layer", IsLayer("N00_001_02_Bottoms_01_CLOTH"), true);
        Check("Onepiece is a layer", IsLayer("N00_010_01_Onepiece_00_CLOTH_02 (Instance)"), true);
        Check("Shoes is a layer", IsLayer("N00_008_01_Shoes_01_CLOTH"), true);
        Check("Accessory (non-CLOTH kind) is a layer", IsLayer("N00_000_00_HairAccessory_00_ACCE"), true);
        Check("Body skin is not a layer", IsLayer("N00_000_00_Body_00_SKIN (Instance)"), false);
        Check("Face is not a layer", IsLayer("N00_000_00_FaceMouth_00_FACE"), false);
        Check("Eye is not a layer", IsLayer("N00_000_00_EyeWhite_00_EYE"), false);
        Check("Hair is not a layer", IsLayer("N00_000_Hair_00_HAIR_01"), false);
        Check("Non-VRoid name is ignored", IsLayer("Material.001"), false);
        Check("CLOTH and CLOTH_01 share a key",
            Key("N00_002_01_Tops_01_CLOTH_01 (Instance)") == Key("N00_002_01_Tops_01_CLOTH"), true);

        Check("CleanName strips (Instance)", LayerDetector.CleanName("Foo (Instance) (Instance)"), "Foo");
        Check("Prettify Onepiece", LayerDetector.Prettify("Onepiece"), "Onepiece");
        Check("Prettify HairAccessory", LayerDetector.Prettify("HairAccessory"), "Hair Accessory");
        Check("Prettify my_jacket", LayerDetector.Prettify("my_jacket"), "My Jacket");
        Check("Prettify empty", LayerDetector.Prettify(""), "Layer");

        Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
        return failures == 0 ? 0 : 1;
    }
}
