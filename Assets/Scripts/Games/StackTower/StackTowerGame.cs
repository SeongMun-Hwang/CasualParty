using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StackTowerGame : MonoBehaviour
{
    public enum RunState { Moving, Falling, GameOver }
    public static StackTowerGame Instance { get; private set; }
    public Camera gameCamera;
    public StackTowerBackground background;
    public StackTowerBlock blockPrefab;
    public BlockArray[] shapePool;
    public Transform blockRoot;
    public Button dropButton;
    public Button rotateCounterClockwiseButton, rotateClockwiseButton;
    public TextMeshProUGUI scoreText, bestText, statusText, resultText;
    public RectTransform statusBackdrop;
    public GameObject gameOverPanel;
    [Header("Prototype tuning")]
    public float cellSize = .6f;
    public float baseTop = 0;
    public float spawnHeight = 3.4f;
    public float moveSpeed = 2.8f;
    [Min(0)] public float rotationSpeed = 120f;
    [Tooltip("Snap to the nearest 90-degree angle on release when within this many degrees. Zero disables snapping.")]
    [Range(0, 30)] public float rotationSnapTolerance = 10f;
    [Tooltip("Gap between the bottom banner/safe area and the controls, in Canvas units.")]
    [Min(0)] public float controlsBottomGap = 40f;
    [Min(0)] public float speedPerHeight = .18f;
    [Min(0)] public float maximumMoveSpeed = 7f;
    [Min(.1f)] public float fallBoundaryDepth = 1.5f;
    public float settleSeconds = .55f;
    public Color[] blockColors = { new Color32(43, 166, 220, 255), new Color32(255, 174, 60, 255), new Color32(65, 194, 165, 255), new Color32(157, 117, 222, 255) };
    public RunState State { get; private set; }
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public StackTowerBlock CurrentBlock { get; private set; }
    public IReadOnlyList<StackTowerBlock> PlacedBlocks => placed;
    public float TowerHeight => Mathf.Max(0, top - baseTop);
    public float CurrentMoveSpeed => Mathf.Min(Mathf.Max(moveSpeed, maximumMoveSpeed), moveSpeed + TowerHeight * Mathf.Max(0, speedPerHeight));
    public bool CanRotate => State == RunState.Moving && CurrentBlock != null && !RankingOverlay.IsOpen && !applicationPaused && !GameNavigation.IsTransitioning;
    private readonly List<StackTowerBlock> placed = new List<StackTowerBlock>();
    private readonly List<BlockArray> allowedShapes = new List<BlockArray>();
    private float top, stableTime, motionTime;
    private bool simulationPaused, applicationPaused;
    private StackTowerRotationHold clockwiseHold, counterClockwiseHold;
    private const string SaveFile = "StackTower.json";

    private void LateUpdate()
    {
        if (dropButton == null) return;
        var canvas = dropButton.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.scaleFactor <= 0) return;
        // Reserve space before the native adaptive banner finishes loading too.
        float bannerHeight = AdManager.Instance != null ? AdManager.Instance.BannerHeightPixels : 0;
        if (bannerHeight <= 0) bannerHeight = Screen.width * (90f / 320f);
        float bottomInset = Mathf.Max(0, Screen.safeArea.yMin);
        float y = GetControlsCenterY(bannerHeight, bottomInset, canvas.scaleFactor,
            ((RectTransform)dropButton.transform).rect.height, controlsBottomGap);
        SetBottomY((RectTransform)dropButton.transform, y);
        if (rotateCounterClockwiseButton != null) SetBottomY((RectTransform)rotateCounterClockwiseButton.transform, y);
        if (rotateClockwiseButton != null) SetBottomY((RectTransform)rotateClockwiseButton.transform, y);
        if (statusText != null) SetBottomY(statusText.rectTransform, y + ((RectTransform)dropButton.transform).rect.height * .5f + 55f);
        if (statusBackdrop != null && statusText != null) SetBottomY(statusBackdrop, statusText.rectTransform.anchoredPosition.y);
    }

    private static void SetBottomY(RectTransform rect, float y)
    {
        var position = rect.anchoredPosition;
        position.y = y;
        rect.anchoredPosition = position;
    }

    public static float GetControlsCenterY(float bannerPixels, float safeBottomPixels, float canvasScale, float buttonHeight, float gap)
    {
        // Include the bottom inset conservatively: native banners can sit above system navigation.
        return (Mathf.Max(0, bannerPixels) + Mathf.Max(0, safeBottomPixels)) / Mathf.Max(.001f, canvasScale)
            + Mathf.Max(0, gap) + buttonHeight * .5f;
    }

    public float GetCameraBaselineY()
    {
        if (statusText == null || gameCamera == null || gameCamera.pixelHeight <= 0) return 5;
        var canvas = statusText.GetComponentInParent<Canvas>();
        if (canvas == null) return 5;
        float floorPixels = (statusText.rectTransform.anchoredPosition.y + statusText.rectTransform.rect.height * .5f + 60) * canvas.scaleFactor;
        return Mathf.Min(5, baseTop + gameCamera.orthographicSize * (1 - 2 * floorPixels / gameCamera.pixelHeight));
    }

    [Serializable] public sealed class Record { public int bestFloors; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        clockwiseHold = rotateClockwiseButton != null ? rotateClockwiseButton.GetComponent<StackTowerRotationHold>() : null;
        counterClockwiseHold = rotateCounterClockwiseButton != null ? rotateCounterClockwiseButton.GetComponent<StackTowerRotationHold>() : null;
        if (SettingsManager.Instance == null) new GameObject("Settings Manager").AddComponent<SettingsManager>();
        if (UI_Functions.Instance == null) new GameObject("Navigation").AddComponent<UI_Functions>();
    }

    private void Start() => StartGame();

    public void StartGame()
    {
        clockwiseHold?.CancelHold(); counterClockwiseHold?.CancelHold();
        foreach (Transform child in blockRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        placed.Clear(); CurrentBlock = null;
        Score = 0; State = RunState.Moving; top = baseTop; motionTime = 0; simulationPaused = false;
        BestScore = Mathf.Max(0, SaveManager.LoadData<Record>(SaveFile).bestFloors);
        scoreText.text = "0"; bestText.text = BestScore.ToString();
        gameOverPanel.SetActive(false);
        LateUpdate();
        gameCamera.transform.position = new Vector3(0, GetCameraBaselineY(), -10);
        if (background != null) background.ResetAltitude(gameCamera.transform.position.y);
        allowedShapes.Clear();
        if (shapePool != null)
            foreach (var shape in shapePool)
                if (StackTowerBlock.IsConnectedShape(shape) && !allowedShapes.Contains(shape)) allowedShapes.Add(shape);
        if (allowedShapes.Count == 0) { EndRun("NO BLOCK SHAPES AVAILABLE"); return; }
        SpawnBlock();
    }

    private void Update()
    {
        bool pause = RankingOverlay.IsOpen || applicationPaused || GameNavigation.IsTransitioning || State == RunState.GameOver;
        SetSimulationPaused(pause);
        SetControlsAvailable(!pause && State == RunState.Moving);
        if (pause || CurrentBlock == null) return;
        // Camera movement must never turn intact lower layers into an out-of-bounds failure.
        foreach (var block in placed)
            if (HasFallenOut(block)) { EndRun("A BLOCK FELL!"); return; }
        if (State == RunState.Falling && HasFallenOut(CurrentBlock)) { EndRun("A BLOCK FELL!"); return; }
        RefreshTowerTop();
        float dt = Time.deltaTime;
        if (State == RunState.Moving)
        {
            int rotationInput = (clockwiseHold != null && clockwiseHold.IsHeld ? 1 : 0) - (counterClockwiseHold != null && counterClockwiseHold.IsHeld ? 1 : 0);
            if (rotationInput != 0) RotateHeld(rotationInput, dt);
            motionTime += dt * CurrentMoveSpeed;
            float range = MovementRange();
            float x = Mathf.Sin(motionTime) * range;
            CurrentBlock.Body.position = new Vector2(x, HoverY());
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) TryDrop();
        }
        else
        {
            var body = CurrentBlock.Body;
            bool stable = CurrentBlock.IsSupported && body.linearVelocity.sqrMagnitude < .035f && Mathf.Abs(body.angularVelocity) < 7;
            stableTime = stable ? stableTime + dt : 0;
            if (stableTime >= settleSeconds)
            {
                placed.Add(CurrentBlock);
                RefreshTowerTop();
                Score++;
                scoreText.text = Score.ToString();
                if (Score > BestScore)
                {
                    BestScore = Score; bestText.text = BestScore.ToString();
                    SaveManager.SaveData(SaveFile, new Record { bestFloors = BestScore });
                }
                SettingsManager.Instance?.Vibrate();
                SpawnBlock();
            }
        }
        float targetY = GetCameraBaselineY() + Mathf.Max(0, TowerHeight - 3);
        var position = gameCamera.transform.position;
        position.y = Mathf.Lerp(position.y, targetY, 1 - Mathf.Exp(-4 * dt));
        gameCamera.transform.position = position;
    }

    public void Drop() => TryDrop();
    private void RotateHeld(int direction, float dt)
    {
        CurrentBlock.Body.rotation = Mathf.Repeat(CurrentBlock.Body.rotation - direction * Mathf.Max(0, rotationSpeed) * dt, 360);
        AdjustPositionAfterRotation();
    }

    public void OnRotationReleased()
    {
        if (!CanRotate || (clockwiseHold != null && clockwiseHold.IsHeld) || (counterClockwiseHold != null && counterClockwiseHold.IsHeld)) return;
        float tolerance = Mathf.Clamp(rotationSnapTolerance, 0, 30);
        if (tolerance <= 0) return;
        float angle = CurrentBlock.Body.rotation;
        float target = Mathf.Round(angle / 90f) * 90f;
        if (Mathf.Abs(Mathf.DeltaAngle(angle, target)) > tolerance) return;
        CurrentBlock.Body.rotation = Mathf.Repeat(target, 360);
        AdjustPositionAfterRotation();
    }

    private void AdjustPositionAfterRotation()
    {
        var position = CurrentBlock.Body.position;
        float range = MovementRange();
        float x = Mathf.Clamp(position.x, -range, range);
        // Preserve horizontal position and travel direction when a rotation changes the piece width.
        float phase = Mathf.Asin(Mathf.Clamp(x / range, -1, 1));
        motionTime = Mathf.Cos(motionTime) < 0 ? Mathf.PI - phase : phase;
        CurrentBlock.Body.position = new Vector2(x, HoverY());
    }

    private float HoverY() => top + spawnHeight + CurrentBlock.Size.magnitude * .5f;
    private float MovementRange() => Mathf.Clamp(gameCamera.orthographicSize * gameCamera.aspect - CurrentBlock.RotatedSize.x * .5f - .3f, .2f, 3.2f);

    private void SetControlsAvailable(bool available)
    {
        if (!available) { clockwiseHold?.CancelHold(); counterClockwiseHold?.CancelHold(); }
        dropButton.interactable = available;
        if (rotateCounterClockwiseButton != null) rotateCounterClockwiseButton.interactable = available;
        if (rotateClockwiseButton != null) rotateClockwiseButton.interactable = available;
    }
    public bool TryDrop()
    {
        if (State != RunState.Moving || CurrentBlock == null || RankingOverlay.IsOpen || applicationPaused || GameNavigation.IsTransitioning) return false;
        State = RunState.Falling; stableTime = 0;
        CurrentBlock.Body.bodyType = RigidbodyType2D.Dynamic;
        CurrentBlock.Body.linearVelocity = Vector2.zero;
        CurrentBlock.Body.angularVelocity = 0;
        SetControlsAvailable(false);
        statusText.text = "LANDING...";
        return true;
    }

    private void SpawnBlock()
    {
        State = RunState.Moving;
        CurrentBlock = Instantiate(blockPrefab, new Vector3(0, top + spawnHeight, 0), Quaternion.identity, blockRoot);
        CurrentBlock.Body.bodyType = RigidbodyType2D.Kinematic;
        var color = blockColors != null && blockColors.Length > 0 ? blockColors[Score % blockColors.Length] : Color.cyan;
        CurrentBlock.Configure(allowedShapes[UnityEngine.Random.Range(0, allowedShapes.Count)], UnityEngine.Random.Range(0, 4), cellSize, color);
        CurrentBlock.Body.position = new Vector2(0, HoverY());
        stableTime = 0;
        SetControlsAvailable(true);
        statusText.text = "TAP DROP TO STACK";
    }

    private bool HasFallenOut(StackTowerBlock block) => block != null && block.WorldBounds.max.y < baseTop - Mathf.Max(.1f, fallBoundaryDepth);

    private void RefreshTowerTop()
    {
        top = baseTop;
        foreach (var block in placed) if (block != null) top = Mathf.Max(top, block.WorldBounds.max.y);
    }

    private void EndRun(string reason)
    {
        if (State == RunState.GameOver) return;
        State = RunState.GameOver;
        SetControlsAvailable(false);
        statusText.text = reason;
        resultText.text = Score + (Score == 1 ? " FLOOR" : " FLOORS") + "\nBEST  " + BestScore;
        gameOverPanel.transform.SetAsLastSibling();
        gameOverPanel.SetActive(true);
        SetSimulationPaused(true);
    }

    private void SetSimulationPaused(bool paused)
    {
        if (simulationPaused == paused) return;
        simulationPaused = paused;
        foreach (var block in placed) if (block != null) block.Body.simulated = !paused;
        if (CurrentBlock != null) CurrentBlock.Body.simulated = !paused;
    }

    private void OnApplicationPause(bool paused) => applicationPaused = paused;
    public void RestartGame() => GameNavigation.Load(SceneManager.GetActiveScene().name);
    public void GoToTitle() => GameNavigation.Load("Title");
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
