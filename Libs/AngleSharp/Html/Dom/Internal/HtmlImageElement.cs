namespace AngleSharp.Html.Dom
{
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using AngleSharp.Io.Processors;
    using AngleSharp.Text;
    using System;

    /// <summary>
    /// Represents the image element.
    /// </summary>
    sealed class HtmlImageElement : HtmlElement, IHtmlImageElement
    {
        #region Fields

        private readonly ImageRequestProcessor _request;

        #endregion

        #region ctor
        
        public HtmlImageElement(Document owner, String prefix = null)
            : base(owner, TagNames.Img, prefix, NodeFlags.Special | NodeFlags.SelfClosing)
        {
            _request = new ImageRequestProcessor(owner.Context);
        }

        #endregion

        #region Properties

        public IDownload CurrentDownload => _request?.Download;

        public String ActualSource => IsCompleted ? _request.Source : String.Empty;

        public String SourceSet
        {
            get => this.GetOwnAttribute(AttributeNames.SrcSet);
            set => this.SetOwnAttribute(AttributeNames.SrcSet, value);
        }

        public String Sizes
        {
            get => this.GetOwnAttribute(AttributeNames.Sizes);
            set => this.SetOwnAttribute(AttributeNames.Sizes, value);
        }

        public String Source
        {
            get => this.GetUrlAttribute(AttributeNames.Src);
            set => this.SetOwnAttribute(AttributeNames.Src, value);
        }

        public String AlternativeText
        {
            get => this.GetOwnAttribute(AttributeNames.Alt);
            set => this.SetOwnAttribute(AttributeNames.Alt, value);
        }

        public String CrossOrigin
        {
            get => this.GetOwnAttribute(AttributeNames.CrossOrigin);
            set => this.SetOwnAttribute(AttributeNames.CrossOrigin, value);
        }

        public String UseMap
        {
            get => this.GetOwnAttribute(AttributeNames.UseMap);
            set => this.SetOwnAttribute(AttributeNames.UseMap, value);
        }

        public Int32 DisplayWidth
        {
            get => this.GetOwnAttribute(AttributeNames.Width).ToInteger(OriginalWidth);
            set => this.SetOwnAttribute(AttributeNames.Width, value.ToString());
        }

        public Int32 DisplayHeight
        {
            get => this.GetOwnAttribute(AttributeNames.Height).ToInteger(OriginalHeight);
            set => this.SetOwnAttribute(AttributeNames.Height, value.ToString());
        }

        public Int32 OriginalWidth => _request?.Width ?? 0;

        public Int32 OriginalHeight => _request?.Height ?? 0;

        public Boolean IsCompleted => _request?.IsReady ?? false;

        public Boolean IsMap
        {
            get => this.GetBoolAttribute(AttributeNames.IsMap);
            set => this.SetBoolAttribute(AttributeNames.IsMap, value);
        }

        #endregion

        #region Internal Methods

        internal override void SetupElement()
        {
            base.SetupElement();
            UpdateSource();
        }

        internal void UpdateSource()
        {
            var url = this.GetImageCandidate();

            if (url != null)
            {
                this.Process(_request, url);
            }
        }

        #endregion
    }
}
