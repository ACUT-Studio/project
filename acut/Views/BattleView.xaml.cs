using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace acut.Views;

public partial class BattleView : UserControl
{
    public BattleView()
    {
        InitializeComponent();
    }

    private void AttackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Resources["PlayerAttackStoryboard"] is Storyboard storyboard)
        {
            storyboard.Begin(this, true);
        }
    }
}