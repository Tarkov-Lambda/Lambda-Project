global using FU = Lambda.Core.Main.FactoryUtilities;
global using HU = Lambda.Core.Main.HealthUtilities;
global using H  = Lambda.Core.Main.Helpers;
global using IU = Lambda.Core.Main.ItemUtilities;
global using PU = Lambda.Core.Main.PlayerUtilities;
global using AU = Lambda.Core.Main.AddressUtilities;

global using RU = Lambda.Core.Main.ReplenishmentUtilities;
global using D = Lambda.Shared.Debugging;


// Tarkov
global using SearchableGrid = GClass3117;
global using ItemExtensions = GClass3380;
global using OperationResult = GStruct153;
global using EquipmentBuildClass = GClass3953;

global using InteractionContextHelper = GetActionsClass;
global using AvailableInteractionState = ActionsReturnClass;
global using LocalizationExtensions = GClass2348;
global using ArmorSlot = GClass3125;
global using IInteractive = GInterface177;

global using IItemRelatedView = GInterface179;

global using CameraManager = CameraClass;

global using EftScreenManager = CurrentScreenSingletonClass;
global using NotificationManager = NotificationManagerClass;

// global using static Lambda.Core.Main.Helpers;

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