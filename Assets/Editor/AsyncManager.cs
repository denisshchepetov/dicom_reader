using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityVolumeRendering
{
    public class AsyncManager 
    {
        private static string _asyncDefinition = "USE_ASYNC_LOADING";

        public static void EnableAsync(bool enable)
        {
            BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup activeGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);

            // Enable the ASYNC_LOADING preprocessor definition for standalone target
            List<BuildTargetGroup> buildTargetGroups = new List<BuildTargetGroup>() { activeGroup };
            foreach (BuildTargetGroup group in buildTargetGroups)
            {
                List<string> defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group).Split(';').ToList();
                defines.Remove(_asyncDefinition);
                if (enable)
                    defines.Add(_asyncDefinition);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, String.Join(";", defines));
            }
        }
        public static bool IsAsyncEnabled()
        {
            BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup activeGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);

            HashSet<string> defines = new HashSet<string>(PlayerSettings.GetScriptingDefineSymbolsForGroup(activeGroup).Split(';'));
            return defines.Contains(_asyncDefinition);
        }
    }
}
