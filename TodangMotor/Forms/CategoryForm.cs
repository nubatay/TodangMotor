using System;
using System.Drawing;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    /// <summary>
    /// Single-record Category editor popup.
    /// Add mode: CategoryForm()
    /// Edit mode: CategoryForm(existingCategory)
    /// </summary>
    public class CategoryForm : ShellForm
    {
        private readonly CategoryService _categoryService;

        private readonly Category? _categoryToEdit;
        private bool IsEditMode => _categoryToEdit != null;

        private RoundedTextBox _txtCategoryName;
        private Button _btnSave;
        private Button _btnCancel;
        private Label _lblStatus;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public CategoryForm(Category? categoryToEdit = null)
        {
            _categoryService = new CategoryService(new CategoryRepository());
            _categoryToEdit = categoryToEdit;

            HeaderTitle = IsEditMode ? "Edit Category" : "Add Category";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 280);
            MinimumSize = new Size(500, 260);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

            Load += CategoryForm_Load;
            KeyDown += CategoryForm_KeyDown;
        }

        // ============================================================
        // LAYOUT
        // ============================================================

        private void BuildLayout()
        {
            // ---- Footer panel (docked bottom, holds buttons) ----
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                BackColor = Theme.Background,
                Padding = new Padding(0)
            };

            const int btnW = 110;
            const int btnH = 40;

            _btnSave = UiFactory.CreateButton("Save", UiFactory.ButtonStyle.Primary, btnW, btnH);
            _btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSave.Click += async (s, e) => await SaveAsync();

            _btnCancel = UiFactory.CreateButton("Cancel", UiFactory.ButtonStyle.Ghost, btnW, btnH);
            _btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            footer.Controls.Add(_btnSave);
            footer.Controls.Add(_btnCancel);

            footer.Resize += (s, e) =>
            {
                int rightPad = 32;
                _btnSave.Location = new Point(
                    footer.ClientSize.Width - rightPad - btnW, 14);
                _btnCancel.Location = new Point(
                    footer.ClientSize.Width - rightPad - btnW - 8 - btnW, 14);
            };

            // ---- Content area above the footer ----
            const int padX = 32;
            const int labelY = 30;
            const int fieldY = 56;

            int fieldW = ClientSize.Width - padX * 2;

            var lblName = new Label
            {
                Text = "Category Name:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(220, 18),
                Location = new Point(padX, labelY),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _txtCategoryName = UiFactory.CreateTextBox(fieldW);
            _txtCategoryName.Location = new Point(padX, fieldY);
            _txtCategoryName.MaxLength = 100;

            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(padX, fieldY + 50),
                Size = new Size(fieldW, 44),
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent
            };

            ContentPanel.Controls.Add(lblName);
            ContentPanel.Controls.Add(_txtCategoryName);
            ContentPanel.Controls.Add(_lblStatus);
            ContentPanel.Controls.Add(footer);
        }

        // ============================================================
        // LOAD
        // ============================================================

        private void CategoryForm_Load(object? sender, EventArgs e)
        {
            Animator.SlideFadeInForm(this, 180, 12);

            if (IsEditMode)
            {
                _txtCategoryName.Text = _categoryToEdit!.CategoryName ?? string.Empty;
                _txtCategoryName.FocusInput();
                _txtCategoryName.SelectAll();
            }
            else
            {
                _txtCategoryName.FocusInput();
            }
        }

        // ============================================================
        // SAVE
        // ============================================================

        private async System.Threading.Tasks.Task SaveAsync()
        {
            SetBusy(true);
            try
            {
                (bool Success, string ErrorMessage) result;

                if (IsEditMode)
                {
                    result = await _categoryService.UpdateCategoryAsync(
                        _categoryToEdit!.CategoryId,
                        _txtCategoryName.Text);
                }
                else
                {
                    result = await _categoryService.AddCategoryAsync(_txtCategoryName.Text);
                }

                if (result.Success)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    ShowError(result.ErrorMessage);
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // EVENTS
        // ============================================================

        private void CategoryForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private void SetBusy(bool busy)
        {
            _btnSave.Enabled = !busy;
            _btnCancel.Enabled = !busy;
            _txtCategoryName.Enabled = !busy;
        }

        private void ShowError(string message)
        {
            _lblStatus.ForeColor = Theme.Danger;
            _lblStatus.Text = message ?? string.Empty;
        }
    }
}