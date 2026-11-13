using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

// Toca música e efeitos passando pelo AudioMixer e guarda os volumes das opções (PlayerPrefs).
// Criado sozinho na primeira vez que alguém usa (AudioManager.Sfx(...), etc.) e sobrevive
// ao recarregar a cena, então a música não reinicia ao tentar de novo.
public class AudioManager : MonoBehaviour
{
    private const int SfxVoices = 10; // quantos efeitos podem tocar ao mesmo tempo
    private const int LongVoices = 4; // sons que podem ser PARADOS no meio (ex.: ficha caindo)

    private static readonly string[] ExposedParams = { "MasterVolume", "MusicVolume", "SfxVolume", "UiVolume" };
    private static readonly float[] DefaultVolumes = { 0.8f, 0.6f, 0.8f, 0.8f };

    private static AudioManager instance;

    private AudioMixer mixer;
    private AudioSource music, intro, ui;
    private double loopStartDsp; // quando o loop deve começar (relógio do áudio)
    private AudioSource[] sfx;
    private AudioSource[] longSfx;
    private int nextVoice;
    private Coroutine musicFade;
    private readonly float[] volumes = new float[4];

    // Equalização: ganho de cada clipe (0..1) + reforço fixo em dB nos grupos SFX e UI.
    private static readonly System.Collections.Generic.Dictionary<AudioClip, float> gains =
        new System.Collections.Generic.Dictionary<AudioClip, float>();
    private static float makeupDb;

    // Chamado pelo GameManager com os valores do GameConfig.
    public static void SetLevels(System.Collections.Generic.IEnumerable<ClipGain> clipGains, float makeup)
    {
        gains.Clear();
        if (clipGains != null)
            foreach (var g in clipGains) if (g != null && g.clip != null) gains[g.clip] = g.gain;
        makeupDb = makeup;
        if (instance != null) { instance.ApplyVolume(AudioBus.Sfx); instance.ApplyVolume(AudioBus.Ui); }
    }

    private static float Gain(AudioClip clip) => gains.TryGetValue(clip, out float g) ? g : 1f;

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
        longSfx = new AudioSource[LongVoices];
        for (int i = 0; i < LongVoices; i++) longSfx[i] = NewSource("SfxLong" + i, AudioBus.Sfx);

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
        if (v > 0.0001f && (bus == AudioBus.Sfx || bus == AudioBus.Ui)) db += makeupDb; // reforço da equalização
        mixer.SetFloat(ExposedParams[(int)bus], db);
    }

    // ===================== Tocar =====================
    // Efeito do jogo (pausa junto com o jogo). pitchJitter varia o tom um pouco para não cansar.
    // "pitch" = tom base (1 = normal, 0.8 = mais grave, 1.2 = mais agudo).
    public static void Sfx(AudioClip clip, float volume = 1f, float pitchJitter = 0.06f, float pitch = 1f)
    {
        if (clip == null) return;
        var self = Instance;
        var src = self.sfx[self.nextVoice];
        self.nextVoice = (self.nextVoice + 1) % SfxVoices;
        src.pitch = pitch + Random.Range(-pitchJitter, pitchJitter);
        src.PlayOneShot(clip, volume * Gain(clip));
    }

    // Igual ao Sfx, mas ignora se o MESMO clipe tocou há menos de "minInterval" segundos (tempo real).
    // Útil quando muitas coisas acontecem juntas (7 fichas quebrando, dezenas de cartas caindo):
    // sem isso o som estoura e ocupa todas as vozes.
    private readonly System.Collections.Generic.Dictionary<AudioClip, float> lastPlayed =
        new System.Collections.Generic.Dictionary<AudioClip, float>();

    public static void SfxThrottled(AudioClip clip, float minInterval, float volume = 1f, float pitchJitter = 0.06f, float pitch = 1f)
    {
        if (clip == null) return;
        var self = Instance;
        float now = Time.unscaledTime;
        if (self.lastPlayed.TryGetValue(clip, out float last) && now - last < minInterval) return;
        self.lastPlayed[clip] = now;
        Sfx(clip, volume, pitchJitter, pitch);
    }

    // Toca um som que pode ser interrompido depois (guarde o AudioSource devolvido e chame StopSfx).
    // Usa uma voz livre das 4 "longas"; se todas estiverem ocupadas, não toca (devolve null).
    public static AudioSource SfxStoppable(AudioClip clip, float volume = 1f, float pitchJitter = 0.06f)
    {
        if (clip == null) return null;
        var self = Instance;
        foreach (var src in self.longSfx)
        {
            if (src.isPlaying) continue;
            src.clip = clip;
            src.volume = volume * Gain(clip);
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.Play();
            return src;
        }
        return null;
    }

    // Para um som tocado com SfxStoppable (fade bem curto para não dar "clique" no corte).
    public static void StopSfx(AudioSource src)
    {
        if (src == null || instance == null || !src.isPlaying) return;
        instance.StartCoroutine(instance.FadeOut(src, 0.06f));
    }

    private IEnumerator FadeOut(AudioSource src, float seconds)
    {
        float start = src.volume;
        AudioClip playing = src.clip;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            if (src.clip != playing) yield break; // a voz já foi reaproveitada por outro som
            src.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }
        if (src.clip == playing) src.Stop();
    }

    // Sorteia um clipe da lista (variações do mesmo som, ex.: match_1..3).
    public static void SfxRandom(AudioClip[] clips, float volume = 1f, float pitchJitter = 0.06f)
    {
        if (clips == null || clips.Length == 0) return;
        Sfx(clips[Random.Range(0, clips.Length)], volume, pitchJitter);
    }

    // Som de interface (cliques, menus). Toca mesmo com o jogo pausado.
    public static void Ui(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        Instance.ui.PlayOneShot(clip, volume * Gain(clip));
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
