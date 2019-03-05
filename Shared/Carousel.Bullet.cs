namespace Zebble.Plugin
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    partial class Carousel
    {
        public readonly Stack BulletsContainer;

        async Task CreateBulletContainer()
        {
            await Add(BulletsContainer);

            foreach (var c in BulletsContainer.AllChildren<Bullet>())
                await c.SetPseudoCssState("active", set: false);

            await (BulletsContainer.AllChildren<Bullet>().Skip(CurrentSlideIndex)
                  .FirstOrDefault()?.SetPseudoCssState("active", set: true)).OrCompleted();
        }

        async Task AddBullet()
        {
            await BulletsContainer.Add(new Bullet());

            if (!BulletsContainer.Visible && BulletsContainer.CurrentChildren.Count() > 1)
                BulletsContainer.Visible(value: true);
        }

        async Task ApplySelectedBullet()
        {
            var bullets = BulletsContainer.AllChildren<Bullet>().ToList();

            var current = bullets.Skip(CurrentSlideIndex).FirstOrDefault();
            if (current == null) return;

            foreach (var c in bullets)
                await c.SetPseudoCssState("active", c == current);
        }

        void PositionBullets()
        {
            BulletsContainer.Y.BindTo(Height, BulletsContainer.Height, BulletsContainer.Margin.Bottom, (x, y, mb) => x - y - mb);
        }

        async Task RemoveLastBullet()
        {
            var bullet = BulletsContainer.CurrentChildren.LastOrDefault();
            if (bullet != null)
                await BulletsContainer.Remove(bullet);

            if (!BulletsContainer.Visible && BulletsContainer.CurrentChildren.Count() > 1)
                BulletsContainer.Visible(value: true);
        }

        void SetHighlightedBullet(int oldIndex, int currentIndex)
        {
            var bullets = BulletsContainer.AllChildren<Bullet>().ToList();
            if (bullets.Count == 0) return;

            var oldBullet = bullets.ElementAtOrDefault(oldIndex);
            var currentBullet = bullets.ElementAt(currentIndex);

            oldBullet?.SetPseudoCssState("active", set: false).RunInParallel();
            currentBullet?.SetPseudoCssState("active", set: true).RunInParallel();
        }

        public class Bullet : Canvas { }
    }
}