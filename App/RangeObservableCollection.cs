using System.Collections.ObjectModel;

namespace FoodStreetAudioGuide
{
    public class RangeObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotifications;

        // Hàm `ReplaceRange`: xử lý logic liên quan trong file hiện tại.
        public void ReplaceRange(IEnumerable<T> items)
        {
            _suppressNotifications = true;
            try
            {
                Items.Clear();
                foreach (var item in items)
                {
                    Items.Add(item);
                }
            }
            finally
            {
                _suppressNotifications = false;
            }

            OnCollectionChanged(new System.Collections.Specialized.NotifyCollectionChangedEventArgs(
                System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
        }

        // Hàm `AddRange`: xử lý logic liên quan trong file hiện tại.
        public void AddRange(IEnumerable<T> items)
        {
            _suppressNotifications = true;
            try
            {
                foreach (var item in items)
                {
                    Items.Add(item);
                }
            }
            finally
            {
                _suppressNotifications = false;
            }

            OnCollectionChanged(new System.Collections.Specialized.NotifyCollectionChangedEventArgs(
                System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
        }

        // Hàm `OnCollectionChanged`: xử lý sự kiện hoặc callback liên quan trong file hiện tại.
        protected override void OnCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (_suppressNotifications)
            {
                return;
            }

            base.OnCollectionChanged(e);
        }
    }
}
