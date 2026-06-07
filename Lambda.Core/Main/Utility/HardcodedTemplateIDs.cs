
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

internal static class Hardcode
{
    public const string BOMB_BACKPACK = "628bc7fb408e2b2e9c0801b1";
    public const string DEFUSE_KIT = "544fb5454bdc2df8738b456a";
    public const string STRAP_NVG = "5c066ef40db834001966a595";
    public const string GPNVG = "5c0558060db834001b735271";
    public const string N15 = "5c066e3a0db834001b7353f0";
    public const string HELMET = "5ea17ca01412a1425304d1c0";

    public const string SMOKE_GRENADE = "5c066e3a0db834001b7353f0";
    public const string MOLOTOV_GRENADE = "617fd91e5539a84ec44ce155";

    public const string TRG = "673cab3e03c6a20581028bc1";
    public const string DEAGLE = "669fa39b48fc9f8db6035a0c";

    public const string KNIFE = "5bffdd7e0db834001b734a1a";

    public const string ARMBAND_CT = "5b3f3af486f774679e752c1f";
    public const string ARMBAND_T = "619bddc6c9546643a67df6ee";

    public const string DEFAULT_TAC_RIG = "67ab3f146d7ece17bf0096ff";

    public static IEnumerable<string> GetAllTemplateIDs()
    {
        var values = typeof(Hardcode)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral &&
            !f.IsInitOnly &&
            f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

        return values;
    }
}