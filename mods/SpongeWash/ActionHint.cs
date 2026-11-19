using System.Reflection;
using EvilCore.Inputs;
using EvilCore.Inputs.UI;
using NomadDrive.Features.Interaction.UI;
using UnityEngine;

namespace SpongeWash
{
    // Uma linha "Lavar esponja [K]" no painel de ações do jogo (o mesmo do "Drink [E]").
    // O painel só sabe mostrar ações do Rewired, então a linha é criada à mão com o
    // mesmo prefab e o ícone da tecla vem do mesmo mapa de sprites que o jogo usa.
    internal sealed class ActionHint
    {
        private static readonly FieldInfo RowPrefabField =
            typeof(InputActionPromptsPanel).GetField("inputActionPromptUIPrefab", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo GlyphServiceField =
            typeof(InputActionPromptsPanel).GetField("_inputGlyphService", BindingFlags.Instance | BindingFlags.NonPublic);

        private InputActionPromptsPanel _panel;
        private InputActionPromptUI _row;

        public void SetVisible(bool visible, string keyName, string label)
        {
            if (!visible)
            {
                Hide();
                return;
            }
            if (_row != null)
            {
                // RebuildPrompts do jogo reordena só as linhas dele; mantém a nossa no fim.
                _row.transform.SetAsLastSibling();
                return;
            }
            if (_panel == null)
            {
                _panel = Object.FindAnyObjectByType<InputActionPromptsPanel>(FindObjectsInactive.Include);
            }
            InputActionPromptUI prefab = _panel != null && RowPrefabField != null ? RowPrefabField.GetValue(_panel) as InputActionPromptUI : null;
            if (prefab == null)
            {
                return;
            }
            IInputGlyphService glyphs = GlyphServiceField != null ? GlyphServiceField.GetValue(_panel) as IInputGlyphService : null;
            Sprite keySprite = glyphs?.GetSpriteForElement(keyName);

            _row = Object.Instantiate(prefab, _panel.transform);
            _row.name = "SpongeWashPrompt";
            _row.Initialize(keySprite, keySprite != null ? label : $"{label} [{keyName}]");
            _row.transform.SetAsLastSibling();
            _row.PlayEnter(0.18f, 24f);
        }

        public void Hide()
        {
            if (_row != null)
            {
                Object.Destroy(_row.gameObject);
            }
            _row = null;
        }
    }
}
