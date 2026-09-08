using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Manimal.LoadAmmoAnim.Patches;
using System;
using System.IO;
using System.Reflection;

// the Fika compat assembly is a separate DLL that hard-refs Fika.Core. it lives
// next to this one and gets sideloaded at Awake if Fika is installed. give it
// access to our internals so it can drive the per-player session state directly.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("LoadAmmoAnimClientFika")]

namespace Manimal.LoadAmmoAnim
{
    [BepInPlugin(BuildInfo.ModGuid, "Manimal-LoadAmmoAnim", BuildInfo.Version)]
    // soft-dep on CLA so we load AFTER it. our plugin's GUID sorts alphabetically
    // before CLA's, which means without this hint BepInEx loads us first — and our
    // Awake-time IsInstalled check would then miss CLA in the plugin registry,
    // skipping the compat patches and breaking chained-mag loading.
    [BepInDependency("com.ozen.continuousloadammo", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private const string FikaGuid = "com.fika.core";
        private const string FikaCompatAssemblyFileName = "LoadAmmoAnimClientFika.dll";
        private const string FikaCompatTypeName = "Manimal.LoadAmmoAnim.Fika.FikaCompatModule";

        public static ManualLogSource LogSource;

        private void Awake()
        {
            LogSource = Logger;
            LogSource.LogInfo("LoadAmmoAnim loaded!");

            // LoadMagazineProcess hooks — detect mag-loading sessions + extend the first-bullet
            // delay to fit the bundle's draw clip. driver-side, unrelated to controller dispatch.
            new LoadAmmoAnimDetectPatch().Enable();
            new LoadMagazineProcessDrawDelayPatch().Enable();
            new RaidStartBundleWarmPatch().Enable();

            // dispatch patches. route LoadAmmoBundleItem through LoadAmmoBundleController
            // instead of the engine's default usable-item handlers. mirror of HackerMod's
            // four-patch shim for custom items.
            new SetInHandsBundlePatch().Enable();
            new ClientUsableBundleSmethod11Patch().Enable();
            new HandsControllerAnimationTypeBundlePatch().Enable();
            new UsableBundleInterfaceDispatchPatch().Enable();

            // defensive PWA guards — there's still a brief window during controller
            // teardown/respawn where WeaponRootAnim can be null/destroyed before the
            // new firearm controllers smethod_8 rebinds. these silence the NRE storm
            // for those frames.
            new ProcessEffectorsNullGuardPatch().Enable();
            new VisualPassNullGuardPatch().Enable();

            if (ContinuousLoadAmmoCompat.IsInstalled)
                ContinuousLoadAmmoCompat.EnablePatches();

            // sideload the Fika compat assembly only when Fika is installed. doing
            // it via reflection means the main DLL never resolves Fika.Core types,
            // so non-Fika setups load cleanly with no missing-dependency errors.
            if (Chainloader.PluginInfos.ContainsKey(FikaGuid))
                LoadFikaCompat();
        }

        private static void LoadFikaCompat()
        {
            try
            {
                var here = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var dllPath = Path.Combine(here, FikaCompatAssemblyFileName);
                if (!File.Exists(dllPath))
                {
                    LogSource.LogWarning(
                        $"[LoadAmmoAnim] Fika detected but compat DLL missing at {dllPath}. " +
                        "multiplayer animations wont sync. reinstall the mod to restore the compat assembly.");
                    return;
                }

                var asm = Assembly.LoadFrom(dllPath);
                var type = asm.GetType(FikaCompatTypeName);
                if (type == null)
                {
                    LogSource.LogError($"[LoadAmmoAnim] couldnt find {FikaCompatTypeName} in {FikaCompatAssemblyFileName}.");
                    return;
                }

                var enable = type.GetMethod("Enable", BindingFlags.Public | BindingFlags.Static);
                if (enable == null)
                {
                    LogSource.LogError($"[LoadAmmoAnim] {FikaCompatTypeName}.Enable() not found.");
                    return;
                }

                enable.Invoke(null, null);
            }
            catch (Exception ex)
            {
                LogSource.LogError($"[LoadAmmoAnim] Fika compat load failed: {ex}");
            }
        }
    }
}
