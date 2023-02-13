﻿namespace Zebble.Plugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Olive;

    public class RecyclerCarousel<TSource, TSlideTemplate> : RecyclerCarousel<TSource>
         where TSlideTemplate : View, IRecyclerCarouselSlide<TSource>, new()
         where TSource : class
    {
        protected override Type GetTemplateType(Type objectType) => typeof(TSlideTemplate);
    }

    public abstract class RecyclerCarousel<TSource> : Carousel where TSource : class
    {
        bool IsInitialized, IsInitializingSlides = true;
        TSource[] dataSource = new TSource[0];
        string LatestRenderedRange;
        Dictionary<Type, List<View>> SlideRecycleBins = new Dictionary<Type, List<View>>();
        List<View> BulletRecycleBin = new List<View>();

        public RecyclerCarousel() => SlideWidthChanged.Event += OnSlideWidthChanged;

        List<View> SlideRecycleBin(Type templateType) => SlideRecycleBins.GetOrAdd(templateType, () => new List<View>());

        /// <summary>
        /// The returned type must implement IRecyclerCarouselSlide<TSource> and have a public constructor.
        /// </summary>
        protected abstract Type GetTemplateType(Type objectType);

        public IEnumerable<TSource> DataSource
        {
            get => dataSource;
            set
            {
                if (IsInitialized)
                {
                    Log.For(this)
                        .Error("RecyclerCarousel.DataSource should not be set once it's initialized. Call UpdateDataSource() instead.");

                    UpdateDataSource(value).RunInParallel();
                }
                else dataSource = value.ToArray();

                LatestRenderedRange = null;
            }
        }

        public async Task UpdateDataSource(IEnumerable<TSource> data)
        {
            dataSource = data.OrEmpty().ToArray();

            AdjustContainerWidth();

            if (!IsInitialized) return;

            while (IsInitializingSlides) await Task.Delay(Animation.OneFrame);
            IsInitializingSlides = true;

            await UIWorkBatch.Run(async () =>
            {
                var toRecycle = OrderedSlides.ToArray();
                foreach (var slide in toRecycle) await MoveToRecycleBin(slide);

                await CreateSufficientSlides();
                await UpdateBullets();
                await ShowFirst(animate: false);
            });
        }

        async Task UpdateBullets()
        {
            if (!ShowBullets) return;

            var toAdd = dataSource.Length - BulletsContainer.AllChildren.Count;

            if (toAdd >= 0) for (var i = 0; i < toAdd; i++) await AddBullet();
            else for (var i = 0; i > toAdd; i--)
                {
                    var bullet = BulletsContainer.AllChildren.LastOrDefault();
                    if (bullet == null) continue;

                    await bullet.IgnoredAsync();
                    await bullet.MoveTo(Root);
                    BulletRecycleBin.Add(bullet);
                }
        }

        protected override bool ShouldAddBulletWithSlide() => false; // Bullets are added with data source

        async Task CreateSufficientSlides()
        {
            IsInitializingSlides = true;

            try
            {
                while (true)
                {
                    var created = SlidesContainer.AllChildren.Count;

                    var slideX = InternalSlideWidth * created;
                    if (slideX > ActualWidth + InternalSlideWidth) return;

                    var nextItem = dataSource.ElementAtOrDefault(created);
                    if (nextItem == null) return;

                    await CreateSlide(nextItem);
                }
            }
            finally
            {
                IsInitializingSlides = false;
            }
        }

        async Task MoveToRecycleBin(View slide)
        {
            await slide.IgnoredAsync();
            await slide.MoveTo(Root);

            var template = GetTemplate(slide)?.GetType();

            if (template != null)
                SlideRecycleBin(template).Add(slide);
        }

        public override async Task OnPreRender()
        {
            await base.OnPreRender();
            await CreateSufficientSlides();
            await UpdateBullets();
            IsInitialized = true;
        }

        async Task<View> CreateSlide(TSource item)
        {
            var templateType = GetTemplateType(item.GetType());
            if (templateType == null) return null;

            var bin = SlideRecycleBin(templateType);

            var result = bin.FirstOrDefault();

            await UIWorkBatch.Run(async () =>
             {
                 if (result != null)
                 {
                     bin.Remove(result);
                     Item(result).Set(item);
                     var newX = dataSource.IndexOf(item) * SlideWidth;
                     result.X(newX);
                     await result.MoveTo(SlidesContainer);
                     await result.IgnoredAsync(false);
                 }
                 else
                 {
                     var slide = (IRecyclerCarouselSlide<TSource>)templateType.CreateInstance();
                     slide.Item.Set(item);
                     result = await AddSlide((View)slide);
                     result.X(result.ActualX);
                 }
             });

            return result;
        }

        protected override async Task AddBullet()
        {
            if (!ShowBullets) return;

            var result = BulletRecycleBin.FirstOrDefault();

            if (result == null)
            {
                if (SlidesContainer.AllChildren.Count <= dataSource.Length) await base.AddBullet();
                return;
            }

            BulletRecycleBin.Remove(result);
            await result.IgnoredAsync(false);
            await result.MoveTo(BulletsContainer);
            if (BulletsContainer.CurrentChildren.Count() > 1) BulletsContainer.Visible();
        }

        IRecyclerCarouselSlide<TSource> GetTemplate(View slide)
        {
            if (slide == null) return null;

            IRecyclerCarouselSlide<TSource> result;

            try
            {
                result = slide?.AllChildren.OfType<IRecyclerCarouselSlide<TSource>>().SingleOrDefault();
            }
            catch
            {
                foreach (var child in slide.AllChildren)
                    if (!(child is IRecyclerCarouselSlide<TSource>))
                        Log.For(this).Error(child.GetType().FullName + " is not " + typeof(IRecyclerCarouselSlide<TSource>).GetProgrammingName());

                result = null;
            }

            if (result == null)
            {
                // How?
            }

            return result;
        }

        Bindable<TSource> Item(View slide) => GetTemplate(slide)?.Item;

        IOrderedEnumerable<View> OrderedSlides => SlidesContainer.AllChildren.OrderBy(x => x.X.CurrentValue);

        void OnSlideWidthChanged()
        {
            if (!IsInitialized) return;

            var index = 0;

            foreach (var item in OrderedSlides.ToArray())
            {
                OnUI(() => item.X(index * SlideWidth).Width(SlideWidth));
                index++;
            }
        }

        void OnUI(Action action) => UIWorkBatch.RunSync(action);

        protected override async Task PrepareForShiftTo(int slideIndex)
        {
            if (dataSource.None()) return;

            while (IsInitializingSlides) await Task.Delay(Animation.OneFrame);

            var min = (slideIndex - 1).LimitMin(0);
            var max = (min + ConcurrentlyVisibleSlides).LimitMax(dataSource.Length);
            await RenderSlides(min, max);
        }

        public Task<View> GetOrCreateCurrentSlide() => RenderSlideAt(CurrentSlideIndex);

        async Task RenderSlides(int min, int max)
        {
            if (LatestRenderedRange == $"{min}-{max}") return;
            LatestRenderedRange = $"{min}-{max}";

            for (var i = min; i <= max; i++)
                await RenderSlideAt(i);
        }

        async Task<View> RenderSlideAt(int slideIndex)
        {
            if (dataSource == null) return null;

            var dataItem = DataSource.ElementAtOrDefault(slideIndex);
            if (dataItem == null) return null;
            var neededTemplate = GetTemplateType(dataItem.GetType());
            if (neededTemplate == null) return null;

            var slideX = slideIndex * SlideWidth;
            var slidesAtPosition = SlidesContainer.AllChildren.Where(v => v.X.CurrentValue == slideX).ToArray();

            View slide = null;

            foreach (var s in slidesAtPosition)
            {
                var template = GetTemplate(s);
                if (template == null) continue;

                if (template.GetType() == neededTemplate)
                {
                    if (slide is null) slide = s;
                    else Log.For(this).Error("Multiple slides at position " + slideX);
                }
                else
                {
                    s.X(-SlideWidth * 2);
                }
            }

            if (slide != null) return slide;

            slide = GetRecyclableSlide(neededTemplate, favourLeft: slideIndex > CurrentSlideIndex);

            if (slide != null)
            {
                Item(slide.X(slideX)).Set(dataItem);
                return slide;
            }
            else
            {
                return (await CreateSlide(dataItem))?.X(slideX);
            }
        }

        View GetRecyclableSlide(Type template, bool favourLeft)
        {
            View fromLeft()
            {
                var farLeft = OrderedSlides.FirstOrDefault(x => GetTemplate(x)?.GetType() == template);
                var keepDownTo = -SlidesContainer.X.CurrentValue - InternalSlideWidth;
                return farLeft?.ActualX < keepDownTo ? farLeft : null;
            }

            View fromRight()
            {
                var farRight = OrderedSlides.LastOrDefault(x => GetTemplate(x)?.GetType() == template);
                var keepUpTo = InternalSlideWidth * ConcurrentlyVisibleSlides - SlidesContainer.X.CurrentValue;
                return farRight?.ActualX > keepUpTo ? farRight : null;
            }

            if (favourLeft) return fromLeft() ?? fromRight();
            else return fromRight() ?? fromLeft();
        }

        public override int CountSlides() => dataSource.Length;
    }
}