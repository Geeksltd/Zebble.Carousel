using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zebble;

namespace Zebble
{
    public class SlidesContainer : Canvas
    {
        internal float SlideWidth;
        public SlidesContainer() => this.Id("SlidesContainer").Height(100.Percent());

        internal void ArrangeSlides(float slideWidth)
        {
            SlideWidth = slideWidth;
            var index = 0;

            foreach (var child in CurrentChildren.OrderBy(x => x.X.CurrentValue))
            {
                child.X(index * slideWidth).Width(slideWidth);
                index++;
            }
        }

        public override Task<TView> AddAt<TView>(int index, TView child, bool awaitNative = false)
        {
            child.X(CurrentChildren.Count() * SlideWidth).Width(SlideWidth);
            return base.AddAt(index, child, awaitNative);
        }
    }
}