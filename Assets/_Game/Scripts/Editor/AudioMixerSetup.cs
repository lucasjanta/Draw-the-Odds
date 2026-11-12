using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

// Cria o AudioMixer do jogo: Master → Music, SFX, UI, com o volume de cada um "exposto"
// (MasterVolume, MusicVolume, SfxVolume, UiVolume) para o código mudar pelas opções.
// A Unity não tem API pública para montar um mixer, então usamos reflexão nas classes internas do Editor.
// Se um dia quebrar, dá para fazer à mão: Create > Audio Mixer, criar os grupos e expor o Volume de cada um.
public static class AudioMixerSetup
{
    public const string MixerPath = "Assets/_Game/Audio/Resources/MainMixer.mixer";

    [MenuItem("Draw the Odds/Create Audio Mixer")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null)
        {
            Debug.Log("[AudioMixerSetup] Mixer já existe: " + MixerPath);
            return;
        }

        System.IO.Directory.CreateDirectory("Assets/_Game/Audio/Resources");
        AssetDatabase.Refresh();

        var editorAsm = typeof(Editor).Assembly;
        Type controllerType = editorAsm.GetType("UnityEditor.Audio.AudioMixerController");
        Type groupType = editorAsm.GetType("UnityEditor.Audio.AudioMixerGroupController");
        Type pathType = editorAsm.GetType("UnityEditor.Audio.AudioGroupParameterPath");
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // 1) Cria o asset do mixer (já vem com o grupo Master e um snapshot)
        object mixer = controllerType.GetMethod("CreateMixerControllerAtPath", all).Invoke(null, new object[] { MixerPath });
        object master = controllerType.GetProperty("masterGroup", all).GetValue(mixer);

        // 2) Cria os grupos filhos do Master
        MethodInfo createGroup = controllerType.GetMethod("CreateNewGroup", all);
        MethodInfo addChild = controllerType.GetMethod("AddChildToParent", all);

        object[] groups = { master, null, null, null };
        string[] names = { "Master", "Music", "SFX", "UI" };
        for (int i = 1; i < names.Length; i++)
        {
            groups[i] = createGroup.Invoke(mixer, new object[] { names[i], false });
            addChild.Invoke(mixer, new[] { groups[i], master });
            // (AddGroupToCurrentView falha num mixer recém-criado sem "views"; não é necessário)
        }

        // 3) Expõe o volume de cada grupo e dá um nome a cada parâmetro
        MethodInfo getVolumeGuid = groupType.GetMethod("GetGUIDForVolume", all);
        MethodInfo addExposed = controllerType.GetMethod("AddExposedParameter", all);
        ConstructorInfo pathCtor = pathType.GetConstructors(all).First(c => c.GetParameters().Length == 2);
        string[] exposedNames = { "MasterVolume", "MusicVolume", "SfxVolume", "UiVolume" };
        for (int i = 0; i < groups.Length; i++)
        {
            object guid = getVolumeGuid.Invoke(groups[i], null);
            object path = pathCtor.Invoke(new[] { groups[i], guid });
            addExposed.Invoke(mixer, new[] { path });
        }

        PropertyInfo exposedProp = controllerType.GetProperty("exposedParameters", all);
        var exposed = (Array)exposedProp.GetValue(mixer);
        for (int i = 0; i < exposed.Length; i++)
        {
            object p = exposed.GetValue(i);
            FieldInfo guidField = p.GetType().GetField("guid", all);
            FieldInfo nameField = p.GetType().GetField("name", all);
            object guid = guidField.GetValue(p);
            for (int g = 0; g < groups.Length; g++)
                if (getVolumeGuid.Invoke(groups[g], null).Equals(guid)) nameField.SetValue(p, exposedNames[g]);
            exposed.SetValue(p, i); // struct: precisa devolver a cópia alterada
        }
        exposedProp.SetValue(mixer, exposed);

        EditorUtility.SetDirty((UnityEngine.Object)mixer);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AudioMixerSetup] Mixer criado: " + MixerPath + " com " + exposed.Length + " parâmetros expostos");
    }
}
