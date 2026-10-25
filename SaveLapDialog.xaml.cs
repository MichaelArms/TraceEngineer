using System.Windows;

namespace TraceEngineer;

public partial class SaveLapDialog : Window
{
    public bool Confirmed { get; private set; }

    public string TrackName => TxtTrack.Text.Trim();
    public string CarName => TxtCar.Text.Trim();
    public string CarClass => (CmbClass.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString() ?? "";
    public string LapDate => TxtDate.Text.Trim();
    public string Note => TxtNote.Text.Trim();

    public SaveLapDialog(string track, string car, string carClass, string date, string lapInfo)
    {
        InitializeComponent();
        TxtTrack.Text = track;
        TxtCar.Text = car;
        TxtDate.Text = date;
        TxtLapInfo.Text = lapInfo;

        if (!string.IsNullOrWhiteSpace(carClass))
        {
            foreach (System.Windows.Controls.ComboBoxItem item in CmbClass.Items)
                if (item.Content.ToString() == carClass) { item.IsSelected = true; break; }
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TrackName))
        {
            MessageBox.Show("Inserisci il nome del circuito.", "Campo mancante",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Confirmed = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        DialogResult = false;
    }
}