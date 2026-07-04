using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Morph.Manager.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Raised when a daemon operation fails, so the page can show the error.</summary>
        public event Action<Exception> Failed;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected void ReportFailure(Exception x)
            => Failed?.Invoke(x);
    }
}
