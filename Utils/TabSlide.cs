using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FileToFiles.Utils;

/// <summary>
/// 选项卡切换的内容滑入动画（位移动画，不使用淡入淡出）。
/// 注意：必须在 SelectionChanged 里同步调用，并且先把偏移量写好再起动画，
/// 否则会先渲染一帧"原位"内容，视觉上就是抖动。
/// </summary>
public static class TabSlide
{
    private const double Offset = 32;
    private const int DurationMs = 200;

    public static void Play(TabControl tab, int previousIndex, int newIndex)
    {
        if (previousIndex == newIndex || newIndex < 0)
        {
            return;
        }

        if (tab.SelectedItem is not TabItem { Content: UIElement content })
        {
            return;
        }

        var direction = newIndex > previousIndex ? 1 : -1;

        if (content.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            content.RenderTransform = transform;
        }

        // 1) 先停掉上一段动画并直接写入起始偏移（本帧还没渲染，用户看不到这一帧）
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        transform.X = direction * Offset;

        // 2) 再从偏移位置滑回 0
        var animation = new DoubleAnimation(direction * Offset, 0, TimeSpan.FromMilliseconds(DurationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }
}
