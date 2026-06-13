using Gazeus.DesafioMatch3.Controllers;
using Gazeus.DesafioMatch3.Models;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class SpecialTileCreationTool : EditorWindow {
    [SerializeField] private VisualTreeAsset m_VisualTreeAsset;
    [SerializeField] private GameController _gameController;

    private Button _clearHorizontalButton;
    private Button _clearVerticalButton;
    private Button _clearAreaButton;
    private Label _statusLabel;

    [MenuItem("Tools/Debug Tools/SpecialTileCreationTool")]
    public static void ShowWindow() {
        SpecialTileCreationTool wnd = GetWindow<SpecialTileCreationTool>();
        wnd.titleContent = new GUIContent("SpecialTileCreationTool");
    }

    private void OnEnable() {
        RefreshGameController();
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable() {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    public void CreateGUI() {
        VisualElement root = rootVisualElement;
        root.Clear();

        if (m_VisualTreeAsset != null) {
            root.Add(m_VisualTreeAsset.Instantiate());
        } else {
            CreateFallbackGui(root);
        }

        _clearHorizontalButton = root.Q<Button>("ClearHorizontalButton");
        _clearVerticalButton = root.Q<Button>("ClearVerticalButton");
        _clearAreaButton = root.Q<Button>("ClearAreaButton");
        _statusLabel = root.Q<Label>("StatusLabel");

        _clearHorizontalButton?.RegisterCallback<ClickEvent>(_ =>
            CreateSpecialTile(TileSpecialType.ClearHorizontal));
        _clearVerticalButton?.RegisterCallback<ClickEvent>(_ =>
            CreateSpecialTile(TileSpecialType.ClearVertical));
        _clearAreaButton?.RegisterCallback<ClickEvent>(_ =>
            CreateSpecialTile(TileSpecialType.ClearArea));

        UpdateButtonState();
    }

    private void CreateFallbackGui(VisualElement root) {
        Label instructions = new(
            "Selecione um tile do board em Play Mode e clique em um dos botoes abaixo para criar um especial.");
        instructions.style.whiteSpace = WhiteSpace.Normal;
        instructions.style.marginBottom = 10f;
        root.Add(instructions);

        root.Add(new Button {
            name = "ClearHorizontalButton",
            text = "Linha"
        });
        root.Add(new Button {
            name = "ClearVerticalButton",
            text = "Coluna"
        });
        root.Add(new Button {
            name = "ClearAreaButton",
            text = "Bomba 3x3"
        });

        Label statusLabel = new() {
            name = "StatusLabel"
        };
        statusLabel.style.marginTop = 8f;
        root.Add(statusLabel);
    }

    private void CreateSpecialTile(TileSpecialType specialType) {
        RefreshGameController();

        if (!CanCreateSpecialTile()) {
            UpdateButtonState();
            return;
        }

        bool created = _gameController.TryCreateSpecialTileAtSelection(specialType);
        SetStatus(created
            ? $"Criado: {GetSpecialTileLabel(specialType)}."
            : "Selecione um tile valido antes de criar o especial.");
    }

    private void RefreshGameController() {
        _gameController = FindFirstObjectByType<GameController>();
    }

    private bool CanCreateSpecialTile() {
        return EditorApplication.isPlaying && _gameController != null;
    }

    private void UpdateButtonState() {
        bool enabled = CanCreateSpecialTile();
        _clearHorizontalButton?.SetEnabled(enabled);
        _clearVerticalButton?.SetEnabled(enabled);
        _clearAreaButton?.SetEnabled(enabled);

        if (!EditorApplication.isPlaying) {
            SetStatus("Entre em Play Mode para usar a ferramenta.");
            return;
        }

        SetStatus(_gameController == null
            ? "GameController nao encontrado na cena."
            : "Selecione um tile no board e escolha o especial.");
    }

    private void SetStatus(string message) {
        if (_statusLabel != null) {
            _statusLabel.text = message;
        }
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state) {
        RefreshGameController();
        UpdateButtonState();
    }

    private static string GetSpecialTileLabel(TileSpecialType specialType) {
        return specialType switch {
            TileSpecialType.ClearHorizontal => "Linha",
            TileSpecialType.ClearVertical => "Coluna",
            TileSpecialType.ClearArea => "Bomba 3x3",
            _ => specialType.ToString()
        };
    }
}