using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    [Header("Sliders")]
    public Slider bgmVolumeSlider;
    public Slider sfxVolumeSlider;
    public Slider vibrationVolumeSlider;

    [Header("Mute Buttons & Icons")]
    public Button bgmMuteButton;
    public Image bgmMuteButtonIcon;
    public Sprite bgmOn, bgmOff;

    public Button sfxMuteButton;
    public Image sfxMuteButtonIcon;
    public Sprite sfxOn, sfxOff;

    public Button vibrationMuteButton;
    public Image vibrationMuteButtonIcon;
    public Sprite vibrationOn, vibrationOff;

    // Mute 전의 값을 기억하기 위한 변수
    private float lastBgmVol = 1f;
    private float lastSfxVol = 1f;
    private float lastVibVol = 1f;

    private bool isUpdatingUI = false;

    private void Awake()
    {
        if (bgmVolumeSlider != null) bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        if (vibrationVolumeSlider != null) vibrationVolumeSlider.onValueChanged.AddListener(OnVibrationVolumeChanged);

        if (bgmMuteButton != null) bgmMuteButton.onClick.AddListener(ToggleBgm);
        if (sfxMuteButton != null) sfxMuteButton.onClick.AddListener(ToggleSfx);
        if (vibrationMuteButton != null) vibrationMuteButton.onClick.AddListener(ToggleVibration);
    }

    private void OnEnable()
    {
        // 패널이 켜질 때 현재 매니저 값으로 초기화
        if (SettingsManager.Instance != null)
        {
            if (SettingsManager.Instance.BgmVolume > 0) lastBgmVol = SettingsManager.Instance.BgmVolume;
            if (SettingsManager.Instance.SfxVolume > 0) lastSfxVol = SettingsManager.Instance.SfxVolume;
            if (SettingsManager.Instance.VibrationValue > 0) lastVibVol = SettingsManager.Instance.VibrationValue;
        }
        UpdateUI();
    }

    #region Slider Events
    private void OnBgmVolumeChanged(float value)
    {
        if (isUpdatingUI) return;
        if (value > 0) lastBgmVol = value;
        SettingsManager.Instance.BgmVolume = value;
        SettingsManager.Instance.BgmMute = (value <= 0);
        UpdateUI();
    }

    private void OnSfxVolumeChanged(float value)
    {
        if (isUpdatingUI) return;
        if (value > 0) lastSfxVol = value;
        SettingsManager.Instance.SfxVolume = value;
        SettingsManager.Instance.SfxMute = (value <= 0);
        UpdateUI();
    }

    private void OnVibrationVolumeChanged(float value)
    {
        if (isUpdatingUI) return;
        if (value > 0) lastVibVol = value;
        SettingsManager.Instance.VibrationValue = value;
        SettingsManager.Instance.VibrationMute = (value <= 0);
        UpdateUI();
    }
    #endregion

    #region Button Events
    private void ToggleBgm()
    {
        bool isMuting = !SettingsManager.Instance.BgmMute;
        if (isMuting)
        {
            // Mute 시: 현재 값을 저장하고 0으로
            if (SettingsManager.Instance.BgmVolume > 0) lastBgmVol = SettingsManager.Instance.BgmVolume;
            SettingsManager.Instance.BgmVolume = 0;
        }
        else
        {
            // Unmute 시: 이전 값 복구
            SettingsManager.Instance.BgmVolume = lastBgmVol;
        }
        SettingsManager.Instance.BgmMute = isMuting;
        UpdateUI();
    }

    private void ToggleSfx()
    {
        bool isMuting = !SettingsManager.Instance.SfxMute;
        if (isMuting)
        {
            if (SettingsManager.Instance.SfxVolume > 0) lastSfxVol = SettingsManager.Instance.SfxVolume;
            SettingsManager.Instance.SfxVolume = 0;
        }
        else
        {
            SettingsManager.Instance.SfxVolume = lastSfxVol;
        }
        SettingsManager.Instance.SfxMute = isMuting;
        UpdateUI();
    }

    private void ToggleVibration()
    {
        bool isMuting = !SettingsManager.Instance.VibrationMute;
        if (isMuting)
        {
            if (SettingsManager.Instance.VibrationValue > 0) lastVibVol = SettingsManager.Instance.VibrationValue;
            SettingsManager.Instance.VibrationValue = 0;
        }
        else
        {
            SettingsManager.Instance.VibrationValue = lastVibVol;
        }
        SettingsManager.Instance.VibrationMute = isMuting;
        UpdateUI();
    }
    #endregion

    private void UpdateUI()
    {
        if (SettingsManager.Instance == null) return;

        isUpdatingUI = true; // Slider.value 변경 시 이벤트 중복 발생 방지

        // 슬라이더 값 업데이트
        if (bgmVolumeSlider != null) bgmVolumeSlider.SetValueWithoutNotify(SettingsManager.Instance.BgmVolume);
        if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(SettingsManager.Instance.SfxVolume);
        if (vibrationVolumeSlider != null) vibrationVolumeSlider.SetValueWithoutNotify(SettingsManager.Instance.VibrationValue);

        // 버튼 아이콘 업데이트
        if (bgmMuteButtonIcon != null) bgmMuteButtonIcon.sprite = SettingsManager.Instance.BgmMute ? bgmOff : bgmOn;
        if (sfxMuteButtonIcon != null) sfxMuteButtonIcon.sprite = SettingsManager.Instance.SfxMute ? sfxOff : sfxOn;
        if (vibrationMuteButtonIcon != null) vibrationMuteButtonIcon.sprite = SettingsManager.Instance.VibrationMute ? vibrationOff : vibrationOn;

        isUpdatingUI = false;
    }
}
