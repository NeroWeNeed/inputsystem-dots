using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
namespace NeroWeNeed.InputSystem.Editor
{

    [CreateAssetMenu(fileName = "InputSystemAssemblyDefinition", menuName = "Input System Assembly Definition", order = 0)]
    public class InputSystemAssemblyDefinition : ScriptableObject
    {
        public InputActionAsset asset;
        public string assemblyPath;
        public string assemblyNamespace;
        public string assemblyName;
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                var path = AssetDatabase.GetAssetPath(this);
                if (path != null)
                {
                    assemblyPath = path.Substring(0, path.LastIndexOf('/'));
                }
            }
            if (assemblyPath.EndsWith("/"))
            {
                assemblyPath = assemblyPath.Substring(0, assemblyPath.Length - 1);
            }
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                assemblyName = $"{Application.productName}.InputSystem.{asset.name}";
            }
        }
    }

}