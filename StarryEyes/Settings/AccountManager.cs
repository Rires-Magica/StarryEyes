﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Livet;
using StarryEyes.Models.Accounting;
using StarryEyes.Models.Databases;

namespace StarryEyes.Settings
{
    public class AccountManager
    {
        private readonly Setting.SettingItem<List<TwitterAccount>> _settingItem;

        private readonly ObservableSynchronizedCollectionEx<TwitterAccount> _accountObservableCollection;

        private readonly ConcurrentDictionary<long, TwitterAccount> _accountCache;

        private readonly Random _random;

        internal AccountManager(Setting.SettingItem<List<TwitterAccount>> settingItem)
        {
            _settingItem = settingItem ?? throw new ArgumentNullException(nameof(settingItem));
            _accountObservableCollection = new ObservableSynchronizedCollectionEx<TwitterAccount>(_settingItem.Value);
            _accountCache = new ConcurrentDictionary<long, TwitterAccount>(
                _accountObservableCollection.Distinct(a => a.Id).ToDictionary(a => a.Id));
            _accountObservableCollection.CollectionChanged += CollectionChanged;
            _random = new Random(Environment.TickCount);
            // initialize DB after system is available.
            App.SystemReady += SynchronizeDb;
        }

        private async void SynchronizeDb()
        {
            var accounts = (await AccountProxy.GetAccountsAsync().ConfigureAwait(false)).ToArray();
            var registered = _accountCache.Keys.ToArray();
            var news = registered.Except(accounts).ToArray();
            var olds = accounts.Except(registered).ToArray();
            await Task.Run(() => Task.WaitAll(
                olds.Select(AccountProxy.RemoveAccountAsync)
                    .Concat(news.Select(AccountProxy.AddAccountAsync))
                    .ToArray())).ConfigureAwait(false);
        }

        public IEnumerable<long> Ids => _accountCache.Keys;

        public ObservableSynchronizedCollectionEx<TwitterAccount> Collection => _accountObservableCollection;

        public bool Contains(long id)
        {
            return _accountCache.ContainsKey(id);
        }

        public TwitterAccount Get(long id)
        {
            TwitterAccount account;
            return _accountCache.TryGetValue(id, out account) ? account : null;
        }

        public TwitterAccount GetRandomOne()
        {
            var accounts = _accountObservableCollection.ToArray();
            return accounts.Length == 0
                ? null
                : accounts[_random.Next(accounts.Length)];
        }

        public TwitterAccount GetRelatedOne(long id)
        {
            if (Setting.Accounts.Contains(id))
            {
                return Get(id);
            }
            var followings = Setting.Accounts.Collection
                                    .Where(a => a.RelationData.Followings.Contains(id))
                                    .ToArray();
            return followings.Length == 0
                ? GetRandomOne()
                : followings[_random.Next(followings.Length)];
        }

        public void RemoveAccountFromId(long id)
        {
            var acc = Get(id);
            if (acc != null) _accountObservableCollection.Remove(acc);
        }

        private void CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var added = e.NewItems?[0] as TwitterAccount;
            var removed = e.OldItems?[0] as TwitterAccount;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (added == null) throw new ArgumentException("added item is null.");
                    _accountCache[added.Id] = added;
                    Task.Run(() => AddAccountSub(added.Id));
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (removed == null) throw new ArgumentException("removed item is null.");
                    TwitterAccount removal;
                    _accountCache.TryRemove(removed.Id, out removal);
                    Task.Run(() => RemoveAccountSub(removed.Id));
                    break;
                case NotifyCollectionChangedAction.Replace:
                    if (added == null) throw new ArgumentException("added item is null.");
                    if (removed == null) throw new ArgumentException("removed item is null.");
                    _accountCache[added.Id] = added;
                    TwitterAccount replacee;
                    _accountCache.TryRemove(removed.Id, out replacee);
                    Task.Run(() => AddAccountSub(added.Id));
                    Task.Run(() => RemoveAccountSub(removed.Id));
                    break;
                case NotifyCollectionChangedAction.Reset:
                    _accountCache.Clear();
                    _accountObservableCollection.ForEach(a => _accountCache[a.Id] = a);
                    Task.Run(AccountProxy.RemoveAllAccountsAsync);
                    break;
            }
            _settingItem.Value = _accountObservableCollection.ToList();
        }

        private async Task AddAccountSub(long id)
        {
            await AccountProxy.AddAccountAsync(id).ConfigureAwait(false);
        }

        private async Task RemoveAccountSub(long id)
        {
            await AccountProxy.RemoveAccountAsync(id).ConfigureAwait(false);
        }
    }
}