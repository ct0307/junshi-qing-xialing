using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

// 通过当前Unity编辑器的原生控制器创建标准Mixer资产，不手写内部YAML。
public static class AudioMixerSetup
{
    private const string Path = "Assets/Resources/Audio/GameMixer.mixer";
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [InitializeOnLoadMethod]
    private static void Schedule()
    { if (!File.Exists(Path)) EditorApplication.delayCall += Ensure; }

    public static void Ensure()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Ensure; return; }
        Directory.CreateDirectory("Assets/Resources/Audio");
        Type controllerType = FindType("UnityEditor.Audio.AudioMixerController");
        Type groupType = FindType("UnityEditor.Audio.AudioMixerGroupController");
        if (controllerType == null || groupType == null) throw new InvalidOperationException("当前Unity没有原生AudioMixerController API");
        var controller = AssetDatabase.LoadAssetAtPath(Path, controllerType);
        if (controller == null)
        {
            controller = (UnityEngine.Object)controllerType.GetMethod("CreateMixerControllerAtPath", Flags).Invoke(null, new object[] { Path });
            object master = controllerType.GetProperty("masterGroup", Flags).GetValue(controller, null);
            object music = NewGroup(controller, "Music");
            object sfx = NewGroup(controller, "SFX");
            SetChildren(master, groupType, music, sfx);
            SetChildren(sfx, groupType, NewGroup(controller, "UI"), NewGroup(controller, "Battle"), NewGroup(controller, "Critical"));
            object snapshot = controllerType.GetProperty("TargetSnapshot", Flags).GetValue(controller, null);
            groupType.GetMethod("SetValueForVolume", Flags).Invoke(master, new object[] { controller, snapshot, -3f });
            EditorUtility.SetDirty((UnityEngine.Object)snapshot);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
        var mixer = controller as AudioMixer;
        foreach (string name in new[] { "Master", "Music", "SFX", "UI", "Battle", "Critical" })
        {
            bool exists = false;
            foreach (AudioMixerGroup group in mixer.FindMatchingGroups(name)) if (group.name == name) exists = true;
            if (!exists) throw new InvalidOperationException("音频混音器缺少组：" + name);
        }
        Debug.Log("原生音频混音器验证通过：Master(-3dB)/Music、SFX/UI、Battle、Critical");
    }

    private static Type FindType(string name)
    { foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) { Type type = assembly.GetType(name); if (type != null) return type; } return null; }
    private static object NewGroup(object controller, string name)
    { return controller.GetType().GetMethod("CreateNewGroup", Flags).Invoke(controller, new object[] { name, false }); }
    private static void SetChildren(object parent, Type groupType, params object[] children)
    {
        Array groups = Array.CreateInstance(groupType, children.Length);
        for (int i = 0; i < children.Length; i++) groups.SetValue(children[i], i);
        parent.GetType().GetProperty("children", Flags).SetValue(parent, groups, null);
        EditorUtility.SetDirty((UnityEngine.Object)parent);
    }
}
