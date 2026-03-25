global using FU = ifp.arena.bep.Core.FactoryUtilities;
global using HU = ifp.arena.bep.Core.HealthUtilities;
global using H  = ifp.arena.bep.Core.Helpers;
global using IU = ifp.arena.bep.Core.ItemUtilities;
global using PU = ifp.arena.bep.Core.PlayerUtilities;
global using AU = ifp.arena.bep.Core.AddressUtilities;

global using RU = ifp.arena.bep.Core.ReplenishmentUtilities;
global using D = ifp.arena.bep.Core.Debugging;


// Tarkov
global using SearchableGrid = GClass3117;
global using ItemExtensions = GClass3380;
global using OperationResult = GStruct153;
global using EquipmentBuildClass = GClass3953;

// To log D.Dump object name
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public string ParameterName { get; }

        public CallerArgumentExpressionAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }
    }
}