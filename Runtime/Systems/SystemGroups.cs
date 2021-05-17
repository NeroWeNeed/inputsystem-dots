using Unity.Entities;

namespace NeroWeNeed.InputSystem {
    [UpdateInGroup(typeof(InputSystemGroup), OrderFirst = true)]
    public class InputAssetLoaderSystemGroup : ComponentSystemGroup {}
    public class InputSystemGroup : ComponentSystemGroup { }
    [UpdateInGroup(typeof(InputSystemGroup))]
    [UpdateAfter(typeof(InputStructuralChangeSystemGroup))]
    public class InputUpdateSystemGroup : ComponentSystemGroup
    {
        public InputUpdateSystemGroup()
        {
            EnableSystemSorting = false;
        }
    }
    [UpdateInGroup(typeof(InputSystemGroup))]
    public class InputStructuralChangeSystemGroup : ComponentSystemGroup { }
    [UpdateInGroup(typeof(InputStructuralChangeSystemGroup))]
    public class InputInitSystemGroup : ComponentSystemGroup { }
    [UpdateInGroup(typeof(InputStructuralChangeSystemGroup))]
    [UpdateAfter(typeof(InputInitSystemGroup))]
    public class InputDisposeSystemGroup : ComponentSystemGroup { }
}