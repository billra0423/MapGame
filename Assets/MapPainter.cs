using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using MapGame.TextRpg;

public sealed class MapPainter : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    private const int RowCount = 10;
    private const int ColumnCount = 15;
    private const int CanvasHeight = 2048;
    private const int CanvasWidth = 3072;
    private const int MaxUndoSteps = 30;

    private readonly Stack<Dictionary<int, Color32>> undo = new Stack<Dictionary<int, Color32>>();
    private readonly Dictionary<string, MapPoint> mapPoints = new Dictionary<string, MapPoint>();
    private Dictionary<int, Color32> currentStroke;
    private Texture2D drawing;
    private Color32[] pixels;
    private bool erasing;
    private bool dragging;
    private bool panning;
    private bool moveClickCancelled;
    private float zoom = 1f;
    private Vector2 pan;
    [Header("Scene UI references")]
    [SerializeField] private RectTransform mapViewport;
    [SerializeField] private RectTransform mapBoard;
    [SerializeField] private RawImage drawingImage;
    [SerializeField] private GameObject gridOverlay;
    [SerializeField] private RectTransform markerTemplate;
    [Header("Player navigation (scene objects)")]
    [SerializeField] private RpgEventController rpgController;
    [SerializeField] private RectTransform playerMarker;
    [SerializeField] private Text playerCoordinate;
    [SerializeField] private RectTransform[] neighbourHighlights;
    [SerializeField] private bool movementMode = true;
    [SerializeField] private bool followPlayer = true;
    [SerializeField, Range(1f, 20f)] private float playerZoom = 3f;
    private Camera eventCamera;
    private Vector2 lastPanPosition;
    [SerializeField] private Text toolStatus;
    [SerializeField] private Image selectedColorPreview;
    [Header("Brush")]
    [SerializeField] private Color penColor = new Color32(38, 53, 72, 255);
    private bool showGrid = true;
    [SerializeField, Range(2, 40)] private int penSize = 8;
    private Vector2? previousPixel;
    private int dirtyMinX, dirtyMinY, dirtyMaxX, dirtyMaxY;
    private bool savePending;
    private float saveAt;
    private string savePath;

    private static readonly Color32 Clear = new Color32(255, 255, 255, 0);

    private struct MapPoint
    {
        public Vector2 coordinate;
        public Color color;
        public RectTransform view;
    }

    // The upper-left corner is (0, 0); A1's center is (0.5, 0.5).
    public bool SetMapPoint(string id, Vector2 coordinate, Color color)
    {
        if (string.IsNullOrEmpty(id) || float.IsNaN(coordinate.x) || float.IsNaN(coordinate.y) ||
            float.IsInfinity(coordinate.x) || float.IsInfinity(coordinate.y) ||
            coordinate.x < 0f || coordinate.x >= ColumnCount || coordinate.y < 0f || coordinate.y >= RowCount)
            return false;
        if (mapBoard == null || markerTemplate == null) return false;
        if (!mapPoints.TryGetValue(id, out MapPoint point))
        {
            // Only data markers clone the inspector-authored template. The drawing UI is saved in the scene.
            point.view = Instantiate(markerTemplate, mapBoard);
            point.view.name = "Map Point " + id;
            point.view.gameObject.SetActive(true);
        }
        point.coordinate = coordinate;
        point.color = color;
        point.view.anchorMin = point.view.anchorMax = new Vector2(coordinate.x / ColumnCount, 1f - coordinate.y / RowCount);
        point.view.anchoredPosition = Vector2.zero;
        point.view.GetComponent<Image>().color = color;
        point.view.GetComponentInChildren<Text>().text = ((char)('A' + (int)coordinate.x)).ToString() + ((int)coordinate.y + 1);
        mapPoints[id] = point;
        return true;
    }

    public bool SetMapPoint(string id, char column, int row, Color color)
    {
        char upper = char.ToUpperInvariant(column);
        if (upper < 'A' || upper >= 'A' + ColumnCount || row < 1 || row > RowCount) return false;
        return SetMapPoint(id, new Vector2(upper - 'A' + 0.5f, row - 0.5f), color);
    }

    public bool RemoveMapPoint(string id)
    {
        if (string.IsNullOrEmpty(id) || !mapPoints.TryGetValue(id, out MapPoint point)) return false;
        Destroy(point.view.gameObject);
        return mapPoints.Remove(id);
    }

    private void Awake()
    {
        RefreshTools();
        savePath = Path.Combine(Application.persistentDataPath, "player_map.png");
        drawing = new Texture2D(CanvasWidth, CanvasHeight, TextureFormat.RGBA32, false);
        drawing.filterMode = FilterMode.Bilinear;
        pixels = new Color32[CanvasWidth * CanvasHeight];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Clear;
        if (!TryLoadDrawing())
        {
            if (drawing.width != CanvasWidth || drawing.height != CanvasHeight)
            {
                Destroy(drawing);
                drawing = new Texture2D(CanvasWidth, CanvasHeight, TextureFormat.RGBA32, false);
                drawing.filterMode = FilterMode.Bilinear;
            }
            ApplyPixels();
        }
    }

    private void Start()
    {
        if (mapViewport == null || mapBoard == null || drawingImage == null)
        {
            Debug.LogError("Connect the map viewport, board and drawing RawImage in the Inspector.", this);
            enabled = false;
            return;
        }
        drawingImage.texture = drawing;
        Canvas.ForceUpdateCanvases();
        if (followPlayer) zoom = playerZoom;
        UpdateBoardLayout();
    }

    private void LateUpdate() { UpdateBoardLayout(); }

    private void Update()
    {
        if (savePending && Time.unscaledTime >= saveAt) SaveDrawing();
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) && keyboard.zKey.wasPressedThisFrame) Undo();
        if (Mouse.current != null && !Mouse.current.leftButton.isPressed && dragging) FinishStroke();
        if (Mouse.current != null && !Mouse.current.middleButton.isPressed) panning = false;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && savePending) SaveDrawing();
    }

    private void OnDestroy()
    {
        if (savePending) SaveDrawing();
        if (drawing != null) Destroy(drawing);
    }

    public void SelectMove() { FinishStroke(); movementMode = true; RefreshTools(); }
    public void SelectDraw() { FinishStroke(); movementMode = false; erasing = false; RefreshTools(); }
    public void SelectErase() { FinishStroke(); movementMode = false; erasing = true; RefreshTools(); }
    public void IncreasePenSize() { penSize = Mathf.Min(40, penSize + 2); RefreshTools(); }
    public void DecreasePenSize() { penSize = Mathf.Max(2, penSize - 2); RefreshTools(); }
    public void ZoomIn() { SetZoom(zoom * 1.25f); }
    public void ZoomOut() { SetZoom(zoom / 1.25f); }
    public void ToggleGrid() { showGrid = !showGrid; RefreshTools(); }
    public void CenterMap() { followPlayer = false; pan = Vector2.zero; UpdateBoardLayout(); }
    public void CenterOnPlayer() { followPlayer = true; UpdateBoardLayout(); }
    public void SelectColor(string hex)
    {
        if (!ColorUtility.TryParseHtmlString(hex, out Color color)) return;
        FinishStroke();
        color.a = 1f;
        penColor = color;
        movementMode = false;
        erasing = false;
        RefreshTools();
    }
    private void RefreshTools()
    {
        if (gridOverlay != null) gridOverlay.SetActive(showGrid);
        if (selectedColorPreview != null) selectedColorPreview.color = penColor;
        if (toolStatus != null) toolStatus.text = (movementMode ? "Move" : erasing ? "Erase" : "Draw") +
            " | Size " + penSize + " | Grid " + (showGrid ? "On" : "Off");
    }

    private void SetZoom(float value) { zoom = Mathf.Clamp(value, 1f, 20f); UpdateBoardLayout(); }
    public void FitMap() { followPlayer = false; zoom = 1f; pan = Vector2.zero; UpdateBoardLayout(); }

    private void UpdateBoardLayout()
    {
        if (mapViewport == null || mapBoard == null) return;
        Vector2 available = mapViewport.rect.size;
        Vector2 boardSize = mapBoard.rect.size;
        if (boardSize.x <= 0f || boardSize.y <= 0f) return;
        float fit = Mathf.Max(0.001f, Mathf.Min(Mathf.Max(1f, available.x - 64f) / boardSize.x,
            Mathf.Max(1f, available.y - 64f) / boardSize.y));
        float scale = fit * zoom;
        mapBoard.localScale = new Vector3(scale, scale, 1f);
        Vector2 limit = Vector2.Max(Vector2.zero, (boardSize * scale - available) * 0.5f);
        if (followPlayer && rpgController != null)
        {
            Vector2 local = new Vector2(mapBoard.rect.xMin + (rpgController.Column + 0.5f) / ColumnCount * boardSize.x,
                mapBoard.rect.yMax - (rpgController.Row - 0.5f) / RowCount * boardSize.y);
            // Permit blank margin at map edges so the player still stays exactly in the center.
            pan = -local * scale;
        }
        else
        {
            pan.x = Mathf.Clamp(pan.x, -limit.x, limit.x);
            pan.y = Mathf.Clamp(pan.y, -limit.y, limit.y);
        }
        mapBoard.anchoredPosition = pan;
        UpdatePlayerView(scale);
    }

    private void UpdatePlayerView(float boardScale)
    {
        if (rpgController == null) return;
        int column = rpgController.Column, row = rpgController.Row;
        if (playerMarker != null)
        {
            playerMarker.anchorMin = playerMarker.anchorMax = new Vector2((column + 0.5f) / ColumnCount, 1f - (row - 0.5f) / RowCount);
            playerMarker.anchoredPosition = Vector2.zero;
            playerMarker.localScale = Vector3.one / boardScale;
        }
        if (playerCoordinate != null) playerCoordinate.text = $"현재 위치 {(char)('A' + column)}{row}";
        if (neighbourHighlights == null) return;
        int index = 0;
        bool canMove = movementMode && rpgController.Player.IsAlive && !rpgController.Session.HasPendingChoice;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                if (index >= neighbourHighlights.Length) return;
                RectTransform highlight = neighbourHighlights[index++];
                if (highlight == null) continue;
                int c = column + dx, r = row + dy;
                bool visible = canMove && rpgController.IsNeighbour(c, r);
                highlight.gameObject.SetActive(visible);
                if (!visible) continue;
                highlight.anchorMin = new Vector2(c / (float)ColumnCount, 1f - r / (float)RowCount);
                highlight.anchorMax = new Vector2((c + 1f) / ColumnCount, 1f - (r - 1f) / RowCount);
                highlight.anchoredPosition = Vector2.zero;
                highlight.sizeDelta = new Vector2(-3f, -3f);
            }
    }

    private bool GetPixelPosition(PointerEventData e, out Vector2 pixel)
    {
        pixel = default;
        if (mapBoard == null || !RectTransformUtility.RectangleContainsScreenPoint(mapViewport, e.position, e.pressEventCamera)) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapBoard, e.position, e.pressEventCamera, out Vector2 local) || !mapBoard.rect.Contains(local)) return false;
        Rect rect = mapBoard.rect;
        pixel = new Vector2((local.x - rect.xMin) / rect.width * CanvasWidth,
            (local.y - rect.yMin) / rect.height * CanvasHeight);
        return true;
    }

    public void OnPointerDown(PointerEventData e)
    {
        moveClickCancelled = false;
        eventCamera = e.pressEventCamera;
        if (e.button == PointerEventData.InputButton.Middle)
        {
            followPlayer = false;
            panning = RectTransformUtility.ScreenPointToLocalPointInRectangle(mapViewport, e.position, eventCamera, out lastPanPosition);
            return;
        }
        if (e.button != PointerEventData.InputButton.Left || !GetPixelPosition(e, out Vector2 pixel)) return;
        if (movementMode) return; // Movement commits on a click, never on a drag.
        if (undo.Count >= MaxUndoSteps)
        {
            Dictionary<int, Color32>[] history = undo.ToArray();
            undo.Clear();
            for (int i = history.Length - 2; i >= 0; i--) undo.Push(history[i]);
        }
        currentStroke = new Dictionary<int, Color32>();
        undo.Push(currentStroke);
        dragging = true;
        previousPixel = null;
        PaintAt(pixel);
    }

    public void OnBeginDrag(PointerEventData e) { moveClickCancelled = true; }
    public void OnPointerClick(PointerEventData e)
    {
        if (!movementMode || moveClickCancelled || e.button != PointerEventData.InputButton.Left || rpgController == null) return;
        if (!GetPixelPosition(e, out Vector2 pixel)) return;
        if (!RpgMapTravel.TryGetCell(pixel.x / CanvasWidth, 1f - pixel.y / CanvasHeight, out int column, out int row)) return;
        if (rpgController.MoveToCell(column, row))
        {
            followPlayer = true;
            UpdateBoardLayout();
        }
    }
    public void OnDrag(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Middle && panning)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(mapViewport, e.position, eventCamera, out Vector2 local))
            {
                pan += local - lastPanPosition;
                lastPanPosition = local;
                UpdateBoardLayout();
            }
        }
        else if (e.button == PointerEventData.InputButton.Left && dragging)
        {
            if (GetPixelPosition(e, out Vector2 pixel)) PaintAt(pixel);
            else previousPixel = null;
        }
    }
    public void OnPointerUp(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) FinishStroke();
        if (e.button == PointerEventData.InputButton.Middle) panning = false;
    }
    public void OnEndDrag(PointerEventData e) { OnPointerUp(e); }
    public void OnScroll(PointerEventData e) { SetZoom(zoom * (e.scrollDelta.y > 0 ? 1.1f : 0.9f)); }
    private void OnDisable() { FinishStroke(); panning = false; }
    private void FinishStroke()
    {
        if (!dragging) return;
        dragging = false;
        currentStroke = null;
        previousPixel = null;
        QueueSave();
    }

    private void PaintAt(Vector2 point)
    {
        dirtyMinX = CanvasWidth;
        dirtyMinY = CanvasHeight;
        dirtyMaxX = dirtyMaxY = -1;
        point.x = Mathf.Clamp(point.x, 0, CanvasWidth - 1);
        point.y = Mathf.Clamp(point.y, 0, CanvasHeight - 1);
        if (previousPixel.HasValue)
        {
            Vector2 start = previousPixel.Value;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, point) / 2f));
            for (int i = 0; i <= steps; i++) Stamp(Vector2.Lerp(start, point, i / (float)steps));
        }
        else Stamp(point);
        previousPixel = point;
        ApplyDirtyPixels();
    }

    private void Stamp(Vector2 center)
    {
        int radius = erasing ? penSize * 2 : penSize;
        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        for (int y = Mathf.Max(0, cy - radius); y <= Mathf.Min(CanvasHeight - 1, cy + radius); y++)
            for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(CanvasWidth - 1, cx + radius); x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                {
                    int index = y * CanvasWidth + x;
                    if (currentStroke != null && !currentStroke.ContainsKey(index)) currentStroke.Add(index, pixels[index]);
                    pixels[index] = erasing ? Clear : (Color32)penColor;
                    dirtyMinX = Mathf.Min(dirtyMinX, x);
                    dirtyMinY = Mathf.Min(dirtyMinY, y);
                    dirtyMaxX = Mathf.Max(dirtyMaxX, x);
                    dirtyMaxY = Mathf.Max(dirtyMaxY, y);
                }
    }

    public void Undo()
    {
        if (undo.Count == 0) return;
        foreach (KeyValuePair<int, Color32> changed in undo.Pop()) pixels[changed.Key] = changed.Value;
        dragging = false;
        currentStroke = null;
        previousPixel = null;
        ApplyPixels();
        QueueSave();
    }

    private void QueueSave()
    {
        savePending = true;
        saveAt = Time.unscaledTime + 0.5f;
    }

    private bool TryLoadDrawing()
    {
        if (!File.Exists(savePath)) return false;
        try
        {
            byte[] data = File.ReadAllBytes(savePath);
            if (!ImageConversion.LoadImage(drawing, data, false)) return false;
            if (drawing.width == CanvasWidth && drawing.height == CanvasHeight)
            {
                pixels = drawing.GetPixels32();
                return true;
            }
            if (drawing.width == CanvasHeight && drawing.height == CanvasHeight)
            {
                Color32[] oldPixels = drawing.GetPixels32();
                int offsetX = 0;
                for (int y = 0; y < CanvasHeight; y++)
                    System.Array.Copy(oldPixels, y * CanvasHeight, pixels, y * CanvasWidth + offsetX, CanvasHeight);
                Destroy(drawing);
                drawing = new Texture2D(CanvasWidth, CanvasHeight, TextureFormat.RGBA32, false);
                drawing.filterMode = FilterMode.Bilinear;
                ApplyPixels();
                QueueSave();
                return true;
            }
            return false;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Could not load player map: " + ex.Message);
            return false;
        }
    }

    public void SaveDrawing()
    {
        try
        {
            File.WriteAllBytes(savePath, drawing.EncodeToPNG());
            savePending = false;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Could not save player map: " + ex.Message);
        }
    }

    private void ApplyPixels()
    {
        drawing.SetPixels32(pixels);
        drawing.Apply(false);
    }

    private void ApplyDirtyPixels()
    {
        if (dirtyMaxX < dirtyMinX) return;
        int width = dirtyMaxX - dirtyMinX + 1;
        int height = dirtyMaxY - dirtyMinY + 1;
        Color32[] region = new Color32[width * height];
        for (int y = 0; y < height; y++)
            System.Array.Copy(pixels, (dirtyMinY + y) * CanvasWidth + dirtyMinX, region, y * width, width);
        drawing.SetPixels32(dirtyMinX, dirtyMinY, width, height, region);
        drawing.Apply(false);
    }

}
