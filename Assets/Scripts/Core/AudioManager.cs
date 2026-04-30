using UnityEngine;

/// <summary>
/// BGM・SEを管理するオーディオマネージャー
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGMソース")]
    public AudioSource bgmSource;

    [Header("BGMクリップ")]
    [Tooltip("戦闘BGM（404FreezeCode.mp3）")]
    public AudioClip battleBGM;
    [Tooltip("フィールドBGM（省略時は無音）")]
    public AudioClip fieldBGM;

    [Header("音量")]
    [Range(0f, 1f)] public float bgmVolume = 0.7f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }
        bgmSource.loop = true;
        bgmSource.volume = bgmVolume;
        bgmSource.playOnAwake = false;
    }

    public void PlayBattleBGM()
    {
        if (battleBGM == null)
        {
            Debug.LogWarning("[AudioManager] battleBGMが未設定です");
            return;
        }
        if (bgmSource.clip == battleBGM && bgmSource.isPlaying) return;
        bgmSource.clip = battleBGM;
        bgmSource.volume = bgmVolume;
        bgmSource.Play();
        Debug.Log("[AudioManager] バトルBGM再生開始: " + battleBGM.name);
    }

    public void StopBGM()
    {
        if (bgmSource.isPlaying) bgmSource.Stop();
        Debug.Log("[AudioManager] BGM停止");
    }

    public void PlayFieldBGM()
    {
        if (fieldBGM != null)
        {
            if (bgmSource.clip == fieldBGM && bgmSource.isPlaying) return;
            bgmSource.clip = fieldBGM;
            bgmSource.volume = bgmVolume * 0.8f;
            bgmSource.Play();
            Debug.Log("[AudioManager] フィールドBGM再生開始");
        }
        else
        {
            StopBGM();
        }
    }
}
