﻿using System;
using StarryEyes.Anomaly.TwitterApi.DataModels;
using StarryEyes.Filters;
using StarryEyes.Filters.Expressions;
using StarryEyes.Models.Databases;
using StarryEyes.Models.Subsystems;

namespace StarryEyes.Models.Timeline
{
    public sealed class TabModel : TimelineModelBase
    {
        private bool _isActivated;
        public bool IsActivated
        {
            get { return _isActivated; }
            set
            {
                if (value == _isActivated) return;
                _isActivated = value;
                this.QueueInvalidateTimeline();
                IsSubscribeBroadcaster = value;
                if (_filterQuery == null) return;
                if (value)
                {
                    _filterQuery.Activate();
                    _filterQuery.InvalidateRequired += this.QueueInvalidateTimeline;
                }
                else
                {
                    _filterQuery.Deactivate();
                    _filterQuery.InvalidateRequired -= this.QueueInvalidateTimeline;
                }
            }
        }

        private FilterQuery _filterQuery;
        public FilterQuery FilterQuery
        {
            get { return _filterQuery; }
            set
            {
                if (value == _filterQuery) return;
                var old = _filterQuery;
                _filterQuery = value;
                this.QueueInvalidateTimeline();
                if (!this._isActivated) return;
                if (_filterQuery != null)
                {
                    this._filterQuery.Activate();
                    this._filterQuery.InvalidateRequired += this.QueueInvalidateTimeline;
                }
                if (old != null)
                {
                    old.Deactivate();
                    old.InvalidateRequired -= this.QueueInvalidateTimeline;
                }
            }
        }

        #region Tab Parameters

        public bool IsNotifyNewArrivals { get; set; }

        public bool IsShowUnreadCounts { get; set; }

        public string NotifySoundSource { get; set; }

        #endregion

        #region Filtering Control

        private string _filterSql = FilterExpressionBase.ContradictionSql;
        private Func<TwitterStatus, bool> _filterFunc = FilterExpressionBase.Contradiction;

        protected override void PreInvalidateTimeline()
        {
            if (_filterQuery == null)
            {
                _filterSql = FilterExpressionBase.ContradictionSql;
                _filterFunc = FilterExpressionBase.Contradiction;
            }
            else
            {
                _filterSql = FilterQuery.GetSqlQuery();
                _filterFunc = FilterQuery.GetEvaluator();
            }
        }

        protected override bool CheckAcceptStatus(TwitterStatus status)
        {
            return _filterFunc(status);
        }

        #endregion

        #region Notification Chain

        protected override void AddStatus(TwitterStatus status)
        {
            if (IsNotifyNewArrivals)
            {
                NotificationService.NotifyNewArrival(status, this);
            }
            base.AddStatus(status);
        }

        #endregion

        protected override IObservable<TwitterStatus> Fetch(long? maxId, int? count)
        {
            return StatusProxy.FetchStatuses(_filterSql, maxId, count);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            // dispose filter
            IsActivated = false;
        }
    }
}