using System;
using System.Drawing;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    public class CategoryForm : ShellForm
    {
        private readonly CategoryService _categoryService;

        private readonly Category? _categoryToEdit;
        private bool IsEditMode => _categoryToEdit != null;

        private RoundedTextBox _txtCategoryName = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;
        private Label _lblStatus = null!;

        private const int PadX = 32;
        private const int LabelH = 22;
        private const int FieldH = 44;

        public CategoryForm(Category? categoryToEdit = null)
        {
            _categoryService = new CategoryService(new CategoryRepository());
            _categoryToEdit = categoryToEdit;

            HeaderTitle = IsEditMode ? "Edit Category" : "Add Category";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 340);
            MinimumSize = new Size(500, 320);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

            Load += CategoryForm_Load;
            KeyDown += CategoryForm_KeyDown;
        }

        private void BuildLayout()
        {
            // ---------- Footer ----------
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 88,
                BackColor = Theme.Background
            };

            var divider = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Theme.Divider
            };
            footer.Controls.Add(divider);

            int btnW = 140;
            int btnH = UiFactory.PrimaryButtonHeight;

            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(PadX, 22),
                Height = btnH,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _btnSave = UiFactory.CreateButton("Save", UiFactory.ButtonStyle.Primary, btnW, btnH);
            _btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSave.Click += async (s, e) => await SaveAsync();

            _btnCancel = UiFactory.CreateButton("Cancel", UiFactory.ButtonStyle.Ghost, btnW, btnH);
            _btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            footer.Controls.Add(_lblStatus);
            footer.Controls.Add(_btnSave);
            footer.Controls.Add(_btnCancel);

            footer.Resize += (s, e) =>
            {
                _btnSave.Location = new Point(footer.ClientSize.Width - PadX - btnW, 22);
                _btnCancel.Location = new Point(footer.ClientSize.Width - PadX - btnW - 8 - btnW, 22);
                _lblStatus.Width = Math.Max(100,
                    footer.ClientSize.Width - PadX - btnW * 2 - 8 - PadX - 16);
            };

            ContentPanel.Controls.Add(footer);

            // ---------- Body ----------
            int fieldW = ClientSize.Width - PadX * 2;
            int y = 32;

            AddFieldLabel("Category Name", PadX, y, fieldW);

            _txtCategoryName = UiFactory.CreateTextBox(fieldW);
            _txtCategoryName.Location = new Point(PadX, y + LabelH + 4);
            _txtCategoryName.MaxLength = 100;
            _txtCategoryName.Placeholder = "e.g. Engine Parts";
            ContentPanel.Controls.Add(_txtCategoryName);
        }

        private void AddFieldLabel(string text, int x, int y, int width)
        {
            var lbl = new Label
            {
                Text = text,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(x, y),
                Size = new Size(width, LabelH),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(lbl);
        }

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

        private async System.Threading.Tasks.Task SaveAsync()
        {
            _lblStatus.Text = string.Empty;
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

        private void CategoryForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

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