using TodangMotor.Common;

namespace TodangMotor.Forms
{
    /// <summary>
    /// This is a PLACEHOLDER screen for the Cashier.
    /// Right now it just proves that login worked and shows who's logged in.
    /// Later, this will become the real Cashier menu (POS, Sales History
    /// only) — Cashiers must NEVER see cost price, Stock-In, or User
    /// Management, even here.
    /// </summary>
    public class CashierDashboardForm : Form
    {
        private Label lblWelcome;
        private Button btnLogout;

        public CashierDashboardForm()
        {
            // --- Form settings ---
            this.Text = "Todang Motor Parts - Cashier Dashboard";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // --- Welcome label ---
            lblWelcome = new Label
            {
                Text = $"Welcome, {SessionManager.CurrentUser?.FullName} (Cashier)",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(30, 30)
            };

            // --- Logout button ---
            btnLogout = new Button
            {
                Text = "Logout",
                Size = new Size(100, 35),
                Location = new Point(30, 80)
            };
            btnLogout.Click += BtnLogout_Click;

            // --- Add controls to the form ---
            this.Controls.Add(lblWelcome);
            this.Controls.Add(btnLogout);
        }

        private void BtnLogout_Click(object? sender, EventArgs e)
        {
            SessionManager.Logout();
            this.Close();
        }
    }
}