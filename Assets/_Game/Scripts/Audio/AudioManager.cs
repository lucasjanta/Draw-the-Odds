using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

// Toca música e efeitos passando pelo AudioMixer e guarda os volumes das opções (PlayerPrefs).
// Criado sozinho na primeira vez que alguém usa (AudioManager.Sfx(...), etc.) e sobrevive
// ao recarregar a cena, então a música não reinicia ao tentar de novo.
public class AudioManager : MonoBehaviour
{
    private const int SfxVoices = 10; // quantos efeitos podem tocar ao mesmo tempo

    private static readonly string[] ExposedParams = { "MasterVolume", "MusicVolume", "SfxVolume", "UiVolume" };
    private static readonly float[] DefaultVolumes = { 0.8f, 0.6f, 0.8f, 0.8f };

    private static AudioManager instance;

    private AudioMixer mixer;
    private AudioSource music, intro, ui;
    private double loopStartDsp; // quando o loop deve começar (relógio do áudio)
    private AudioSource[] sfx;
    private int nextVoice;
    private Coroutine musicFade;
    private readonly float[] volumes = new float[4];

    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("AudioManager");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<AudioManager>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        mixer = Resources.Load<AudioMixer>("MainMixer");
        if (mixer == null) Debug.LogWarning("[AudioManager] MainMixer não encontrado em Resources (menu Draw the Odds/Create Audio Mixer).");

        music = NewSource("Music", AudioBus.Music);
        music.loop = true;
        music.ignoreListenerPause = true; // a música continua no menu de pausa
        intro = NewSource("MusicIntro", AudioBus.Music);
        intro.ignoreListenerPause = true;

        ui = NewSource("UI", AudioBus.Ui);
        ui.ignoreListenerPause = true;    // cliques do menu de pausa precisam soar

        sfx = new AudioSource[SfxVoices];
        for (int i = 0; i < SfxVoices; i++) sfx[i] = NewSource("Sfx" + i, AudioBus.Sfx);

        for (int i = 0; i < volumes.Length; i++)
            volumes[i] = PlayerPrefs.GetFloat("vol_" + (AudioBus)i, DefaultVolumes[i]);
    }

    // SetFloat no mixer não funciona no Awake (limitação da Unity); por isso aplicamos no Start.
    private void Start()
    {
        for (int i = 0; i < volumes.Length; i++) ApplyVolume((AudioBus)i);
    }

    private AudioSource NewSource(string name, AudioBus bus)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.outputAudioMixerGroup = FindGroup(bus);
        return src;
    }

    private AudioMixerGroup FindGroup(AudioBus bus)
    {
        if (mixer == null) return null;
        string path = bus == AudioBus.Master ? "Master" : "Master/" + (bus == AudioBus.Sfx ? "SFX" : bus == AudioBus.Ui ? "UI" : "Music");
        var groups = mixer.FindMatchingGroups(path);
        return groups.Length > 0 ? groups[0] : null;
    }

    // ===================== Volume (0..1) =====================
    public float GetVolume(AudioBus bus) => volumes[(int)bus];

    public void SetVolume(AudioBus bus, float value)
    {
        volumes[(int)bus] = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("vol_" + bus, volumes[(int)bus]);
        ApplyVolume(bus);
    }

    // O mixer trabalha em decibéis: 1 → 0 dB, 0.5 → −6 dB, 0 → −80 dB (mudo).
    private void ApplyVolume(AudioBus bus)
    {
        if (mixer == null) return;
        float v = volumes[(int)bus];
        float db = v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f;
        mixer.SetFloat(ExposedParams[(int)bus], db);
    }

    // ===================== Tocar =====================
    // Efeito do jogo (pausa junto com o jogo). pitchJitter varia o tom um pouco para não cansar.
    public static void Sfx(AudioClip clip, float volume = 1f, float pitchJitter = 0.06f)
    {
        if (clip == null) return;
        var self = Instance;
        var src = self.sfx[self.nextVoice];
        self.nextVoice = (self.nextVoice + 1) % SfxVoices;
        src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        src.PlayOneShot(clip, volume);
    }

    // Som de interface (cliques, menus). Toca mesmo com o jogo pausado.
    public static void Ui(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        Instance.ui.PlayOneShot(clip, volume);
    }

    // Troca a música com fade (tempo real). Com "introClip", toca a intro uma vez e emenda no loop
    // sem buraco (PlayScheduled). Se o mesmo loop já está tocando, não reinicia.
    public static void Music(AudioClip loopClip, float fadeSeconds = 1f) => Music(null, loopClip, fadeSeconds);

    public static void Music(AudioClip introClip, AudioClip loopClip, float fadeSeconds = 1f)
    {
        var self = Instance;
        if (self.music.clip == loopClip && loopClip != null && (self.music.isPlaying || self.intro.isPlaying)) return;
        if (self.musicFade != null) self.StopCoroutine(self.musicFade);
        self.musicFade = self.StartCoroutine(self.FadeTo(introClip, loopClip, fadeSeconds));
    }

    private IEnumerator FadeTo(AudioClip introClip, AudioClip loopClip, float seconds)
    {
        float half = Mathf.Max(0.01f, seconds * 0.5f);
        float start = music.volume;
        if (music.isPlaying || intro.isPlaying)
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                music.volume = intro.volume = Mathf.Lerp(start, 0f, t / half);
                yield return null;
            }

        music.Stop();
        intro.Stop();
        music.clip = loopClip;
        intro.clip = introClip;
        music.volume = intro.volume = 1f;
        if (loopClip == null) yield break;

        if (introClip != null)
        {
            // Agenda a intro e o loop no relógio do áudio: o loop começa exatamente quando a intro acaba.
            double now = AudioSettings.dspTime + 0.1;
            intro.PlayScheduled(now);
            loopStartDsp = now + (double)introClip.samples / introClip.frequency;
            music.PlayScheduled(loopStartDsp);
        }
        else
        {
            loopStartDsp = 0;
            music.Play();
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime) { music.volume = Mathf.Lerp(0f, 1f, t / half); yield return null; }
            music.volume = 1f;
        }
        musicFade = null;
    }

    // Segurança (navegadores): se o loop agendado não começou na hora, começa agora.
    private void Update()
    {
        if (loopStartDsp > 0 && music.clip != null && !music.isPlaying && !intro.isPlaying
            && AudioSettings.dspTime > loopStartDsp + 0.2)
        {
            music.Play();
            loopStartDsp = 0;
        }
    }
}
