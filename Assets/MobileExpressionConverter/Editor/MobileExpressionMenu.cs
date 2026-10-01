using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace MobileExpressionConverter.Editor
{
    internal static class MobileExpressionMenu
    {
        private const string MenuPath = "GameObject/VRChat/Mobile Expression Converter";

        [MenuItem(MenuPath, true, 49)]
        private static bool Validate(MenuCommand command) => GetAvatar(command) != null;

        [MenuItem(MenuPath, false, 49)]
        private static void AddComponent(MenuCommand command)
        {
            var avatar = GetAvatar(command);
            if (avatar == null) return;

            var settings = avatar.GetComponent<MobileExpressionSettings>();
            if (settings == null) settings = Undo.AddComponent<MobileExpressionSettings>(avatar);
            MobileExpressionSettingsEditor.FillDetectedRenderer(settings);
            Selection.activeGameObject = avatar;
            EditorGUIUtility.PingObject(settings);
        }

        private static GameObject GetAvatar(MenuCommand command)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return null;
            var avatar = command.context as GameObject;
            if (avatar == null)
            {
                if (Selection.gameObjects.Length != 1) return null;
                avatar = Selection.activeGameObject;
            }

            if (avatar == null || EditorUtility.IsPersistent(avatar)
                || (avatar.hideFlags & HideFlags.NotEditable) != 0
                || avatar.GetComponent<VRCAvatarDescriptor>() == null) return null;
            return avatar;
        }
    }
}
