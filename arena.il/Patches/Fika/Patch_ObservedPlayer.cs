using Comfort.Common;
using EFT;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.Packets.Player.Common.SubPackets;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using EFT.HealthSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using EFT.InventoryLogic;
using Fika.Core.Main.HostClasses;
using System.Diagnostics;
using Fika.Core.Main.ClientClasses;

namespace ifp.arena.il.Patches
{
    // I'm sorry child but life is lonely

    internal class ObservedPlayer_CreateObservedPlayer_Transpiler : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            var stateMachineType = typeof(ObservedPlayer)
                .GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(t =>
                    t.GetCustomAttribute<CompilerGeneratedAttribute>() != null &&
                    t.Name.Contains(nameof(ObservedPlayer.CreateObservedPlayer)));

            return stateMachineType?.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);
        }
        [PatchTranspiler]
        public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var factory = AccessTools.Method(typeof(ObservedPlayer_CreateObservedPlayer_Transpiler), nameof(CreateHealthController));
            var myLocal = il.DeclareLocal(typeof(IHealthController));

            var codes = instructions.ToList();
            CodeInstruction storeInstr = null;
            bool replaced = false;

            for (int i = 0; i < codes.Count; i++)
            {
                var instr = codes[i];

                if (!replaced && instr.opcode == OpCodes.Newobj && instr.operand is ConstructorInfo ci && ci.DeclaringType == typeof(ObservedHealthController))
                {
                    replaced = true;

                    // Because the original code compiles down to: new ObservedHealthController(..., profile.Skills)
                    // The instruction immediately prior to this is ldfld 'Skills'. 
                    // We NOP that out, which leaves the full 'Profile' object on the stack as our 4th argument!
                    if (i > 0 && (codes[i - 1].opcode == OpCodes.Ldfld || codes[i - 1].opcode == OpCodes.Callvirt))
                    {
                        codes[i - 1].opcode = OpCodes.Nop;
                        codes[i - 1].operand = null;
                    }

                    // Swap the newobj call with our factory method
                    codes[i] = new CodeInstruction(OpCodes.Call, factory);

                    storeInstr = codes[i + 1];

                    // Store our interface in our custom local, and feed null to Fika's original ObservedHealthController local
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Stloc, myLocal));
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldnull));

                    i += 2;
                    continue;
                }

                if (replaced && storeInstr != null && IsMatchingLoad(instr, storeInstr))
                {
                    // Whenever Fika tries to load its ObservedHealthController local, pop the null and give it our interface
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Pop));
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldloc, myLocal));

                    i += 2;
                    continue;
                }
            }

            return codes;
        }

        private static bool IsMatchingLoad(CodeInstruction load, CodeInstruction store)
        {
            if (store.opcode == OpCodes.Stloc_0 && load.opcode == OpCodes.Ldloc_0) return true;
            if (store.opcode == OpCodes.Stloc_1 && load.opcode == OpCodes.Ldloc_1) return true;
            if (store.opcode == OpCodes.Stloc_2 && load.opcode == OpCodes.Ldloc_2) return true;
            if (store.opcode == OpCodes.Stloc_3 && load.opcode == OpCodes.Ldloc_3) return true;

            if (store.opcode == OpCodes.Stloc_S && load.opcode == OpCodes.Ldloc_S) return Equals(store.operand, load.operand);
            if (store.opcode == OpCodes.Stloc && load.opcode == OpCodes.Ldloc) return Equals(store.operand, load.operand);

            if (store.opcode == OpCodes.Stfld && load.opcode == OpCodes.Ldfld)
            {
                var stField = store.operand as FieldInfo;
                var ldField = load.operand as FieldInfo;
                if (stField != null && ldField != null && stField.Name == ldField.Name) return true;
            }

            return false;
        }

        // Factory Signature updated to accept Profile instead of SkillManager!
        private static IHealthController CreateHealthController(byte[] healthBytes, ObservedPlayer player, InventoryController inventoryController, Profile profile)
        {
            try
            {
                Plugin.Logger.LogInfo($"[Transpiler-Runtime] Inside CreateHealthController! (Player: {profile?.Nickname})");

                if (FikaBackendUtils.IsServer)
                {
                    Plugin.Logger.LogInfo("[Transpiler-Runtime] Creating ServerAuthoritativeHealthController (GClass3010)!");

                    // We can now safely grab the ProfileHealthClass directly from the passed-in profile argument!
                    return new ServerActiveHealthController(profile.Health, player, inventoryController, profile.Skills);
                }

                Plugin.Logger.LogInfo("[Transpiler-Runtime] Creating standard ObservedHealthController.");
                return new ObservedHealthController(healthBytes, player, inventoryController, profile.Skills);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[Transpiler-Runtime] FATAL ERROR IN FACTORY: {ex}");
                throw;
            }
        }
    }
}

