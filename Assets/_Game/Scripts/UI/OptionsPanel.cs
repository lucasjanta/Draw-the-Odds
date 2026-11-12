using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Painel de opções (usado no menu principal e no de pausa): volume Master, Music e Effects,
// e a trilha da gameplay (AUTO = cada chefe com a sua, ou sempre a música 1 / 2).
// "Effects" controla os barramentos SFX e UI juntos.
public class OptionsPanel : MonoBehaviour
{
    private Slider master, music, effects;
    private TextMeshProUGUI masterValue, musicValue, effectsValue, trackLabel;

    public static OptionsPanel Create(Transform parent, Action onBack)
    {
        var panel = UIFactory.MenuPanel("Options", parent, new Vector2(300f, 196f));
        var p = panel.gameObject.AddComponent<OptionsPanel>();

        var title = UIFactory.Text("Title", panel.transform, "OPTIONS", 20f, UIFactory.Gold, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(280f, 26f));

        p.master = p.Row(panel.transform, "MASTER", -44f, AudioBus.Master, out p.masterValue);
        p.music = p.Row(panel.transform, "MUSIC", -70f, AudioBus.Music, out p.musicValue);
        p.effects = p.Row(panel.transform, "EFFECTS", -96f, AudioBus.Sfx, out p.effectsValue);

        // Trilha: botão que alterna AUTO → GAMEPLAY 1 → GAMEPLAY 2 → AUTO...
        var trackName = UIFactory.Text("Track", panel.transform, "TRACK", 11f, Color.white);
        trackName.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, -126f), new Vector2(70f, 16f));
        var trackButton = UIFactory.Button("TrackButton", panel.transform, "", new Vector2(150f, 18f), p.CycleTrack, 10f);
        trackButton.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(92f, -126f), new Vector2(150f, 18f));
        p.trackLabel = trackButton.GetComponentInChildren<TextMeshProUGUI>();

        var back = UIFactory.Button("Back", panel.transform, "BACK", new Vector2(120f, 24f), onBack, 12f);
        back.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(120f, 24f));

        panel.gameObject.SetActive(false);
        return p;
    }

    // Uma linha: nome à esquerda, slider no meio, porcentagem à direita.
    private Slider Row(Transform parent, string label, float y, AudioBus bus, out TextMeshProUGUI valueText)
    {
        var name = UIFactory.Text(label, parent, label, 11f, Color.white);
        name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, y), new Vector2(70f, 16f));

        var value = UIFactory.Text(label + "Value", parent, "", 11f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Right);
        value.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-18f, y), new Vector2(40f, 16f));
        valueText = value;

        var slider = UIFactory.Slider(label + "Slider", parent, new Vector2(150f, 6f), 0f, v => OnChanged(bus, v));
        slider.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(92f, y), new Vector2(150f, 6f));
        return slider;
    }

    private void OnChanged(AudioBus bus, float v)
    {
        var audio = AudioManager.Instance;
        audio.SetVolume(bus, v);
        if (bus == AudioBus.Sfx) audio.SetVolume(AudioBus.Ui, v); // Effects = SFX + UI
        RefreshLabels();
    }

    public void Show(bool show)
    {
        gameObject.SetActive(show);
        if (!show) return;
        var audio = AudioManager.Instance;
        master.SetValueWithoutNotify(audio.GetVolume(AudioBus.Master));
        music.SetValueWithoutNotify(audio.GetVolume(AudioBus.Music));
        effects.SetValueWithoutNotify(audio.GetVolume(AudioBus.Sfx));
        RefreshLabels();
    }

    private void CycleTrack()
    {
        var gm = GameManager.Instance;
        int count = gm != null ? gm.Config.GameplayTracks().Count : 2;
        GameManager.MusicTrackChoice = (GameManager.MusicTrackChoice + 1) % (count + 1);
        if (gm != null) gm.ApplyMusicChoice();
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        int track = GameManager.MusicTrackChoice;
        trackLabel.text = track == 0 ? "◄ AUTO ►" : $"◄ GAMEPLAY {track} ►";
        masterValue.text = Mathf.RoundToInt(master.value * 100f) + "%";
        musicValue.text = Mathf.RoundToInt(music.value * 100f) + "%";
        effectsValue.text = Mathf.RoundToInt(effects.value * 100f) + "%";
    }
}
