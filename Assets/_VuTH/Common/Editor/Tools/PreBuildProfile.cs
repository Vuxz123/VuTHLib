using System;
using UnityEngine;

namespace _Game.Scripts.VuTH.Common.Editor.Tools
{
    [CreateAssetMenu(fileName = "PreBuildProfile", menuName = "VuTH/Tools/PreBuildProfile", order = 1)]
    public class PreBuildProfile : ScriptableObject
    {
        [SerializeReference] private IPreBuildTask[] preBuildTasks;
        
        public void ExecuteAllTasks()
        {
            if (preBuildTasks == null) return;

            foreach (var task in preBuildTasks)
            {
                try
                {
                    task.Execute();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error executing pre-build task {task.GetType().Name}: {ex.Message}");
                }
            }
        }
    }

    internal interface IPreBuildTask
    {
        void Execute();
    }
}