﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Monitorian.Core.Models.Monitor;

namespace Monitorian.Core.ViewModels
{
	public class MonitorViewModel : ViewModelBase
	{
		private readonly AppControllerCore _controller;
		private IMonitor _monitor;

		public MonitorViewModel(AppControllerCore controller, IMonitor monitor)
		{
			this._controller = controller ?? throw new ArgumentNullException(nameof(controller));
			this._monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));

			this._controller.TryLoadNameUnison(DeviceInstanceId, ref _name, ref _isUnison);
		}

		private readonly object _lock = new object();

		internal void Replace(IMonitor monitor)
		{
			if (monitor is null)
				return;

			lock (_lock)
			{
				this._monitor.Dispose();
				this._monitor = monitor;
			}
		}

		public string DeviceInstanceId => _monitor.DeviceInstanceId;
		public string Description => _monitor.Description;
		public byte DisplayIndex => _monitor.DisplayIndex;
		public byte MonitorIndex => _monitor.MonitorIndex;
		public bool IsAccessible => _monitor.IsAccessible;

		#region Name/Unison

		public string Name
		{
			get => _name ?? _monitor.Description;
			set
			{
				if (SetPropertyValue(ref _name, GetValueOrNull(value)))
					_controller.SaveNameUnison(DeviceInstanceId, _name, _isUnison);
			}
		}
		private string _name;

		private static string GetValueOrNull(string value) => !string.IsNullOrWhiteSpace(value) ? value : null;

		public bool IsUnison
		{
			get => _isUnison;
			set
			{
				if (SetPropertyValue(ref _isUnison, value))
					_controller.SaveNameUnison(DeviceInstanceId, _name, _isUnison);
			}
		}
		private bool _isUnison;

		#endregion

		#region Brightness

		public int Brightness
		{
			get => _monitor.Brightness;
			set
			{
				if (_monitor.Brightness == value)
					return;

				SetBrightness(value);

				if (IsSelected)
					_controller.SaveMonitorUserChanged(this);
			}
		}

		public int BrightnessSystemAdjusted => _monitor.BrightnessSystemAdjusted;
		public int BrightnessSystemChanged => Brightness;

		public bool UpdateBrightness(int brightness = -1)
		{
			var isSuccess = false;
			lock (_lock)
			{
				isSuccess = _monitor.UpdateBrightness(brightness);
			}
			if (isSuccess)
			{
				RaisePropertyChanged(nameof(BrightnessSystemChanged)); // This must be prior to Brightness.
				RaisePropertyChanged(nameof(Brightness));
				RaisePropertyChanged(nameof(BrightnessSystemAdjusted));
				OnSuccess();
			}
			else
			{
				OnFailure();
			}
			return isSuccess;
		}

		public void IncrementBrightness()
		{
			IncrementBrightness(10);

			if (IsSelected)
				_controller.SaveMonitorUserChanged(this);
		}

		public void IncrementBrightness(int tickSize, bool isCycle = true)
		{
			int brightness = (Brightness / tickSize) * tickSize + tickSize;
			if (100 < brightness)
				brightness = isCycle ? 0 : 100;

			SetBrightness(brightness);
		}

		public void DecrementBrightness(int tickSize, bool isCycle = true)
		{
			int brightness = (Brightness / tickSize) * tickSize - tickSize;
			if (brightness < 0)
				brightness = isCycle ? 100 : 0;

			SetBrightness(brightness);
		}

		private bool SetBrightness(int brightness)
		{
			var isSuccess = false;
			lock (_lock)
			{
				isSuccess = _monitor.SetBrightness(brightness);
			}
			if (isSuccess)
			{
				RaisePropertyChanged(nameof(Brightness));
				OnSuccess();
			}
			else
			{
				OnFailure();
			}
			return isSuccess;
		}

		#endregion

		#region Controllable

		public bool IsControllable => IsAccessible && (_controllableCount > 0);

		public bool IsLikelyControllable => IsControllable || _isSuccessCalled;
		private bool _isSuccessCalled;

		// This count is for determining IsControllable property.
		// To set this count, the following points need to be taken into account: 
		// - The initial value of IsControllable property should be true (provided IsAccessible is
		//   true) because a monitor is expected to be controllable. Therefore, the initial count
		//   should be greater than 0.
		// - The initial count is intended to give allowance for failures before the first success.
		//   If the count has been consumed without any success, the monitor will be regarded as
		//   uncontrollable at all.
		// - _isSuccessCalled field indicates that the monitor has succeeded at least once.
		//   It essentially needs to be changed only once at the first success.
		// - The normal count gives allowance for failures after the first and succeeding successes.
		//   As long as the monitor continues to succeed, the count will stay at the normal count.
		//   Each time the monitor fails, the count decreases. The decreased count will be reverted
		//   to the normal count when the monitor succeeds again.
		// - The initial count must be smaller than the normal count so that _isSuccessCalled field
		//   will be set at the first success while reducing unnecessary access to the field.
		private short _controllableCount = InitialCount;
		private const short InitialCount = 3;
		private const short NormalCount = 5;

		private void OnSuccess()
		{
			if (_controllableCount < NormalCount)
			{
				var formerCount = _controllableCount;
				_controllableCount = NormalCount;
				if (formerCount <= 0)
					RaisePropertyChanged(nameof(IsControllable));

				_isSuccessCalled = true;
			}
		}

		private void OnFailure()
		{
			if (--_controllableCount == 0)
				RaisePropertyChanged(nameof(IsControllable));
		}

		#endregion

		#region Focus

		public bool IsByKey
		{
			get => _isByKey;
			set
			{
				if (SetPropertyValue(ref _isByKey, value))
					RaisePropertyChanged(nameof(IsSelectedByKey));
			}
		}
		private bool _isByKey;

		public bool IsSelected
		{
			get => _isSelected;
			set
			{
				if (SetPropertyValue(ref _isSelected, value))
					RaisePropertyChanged(nameof(IsSelectedByKey));
			}
		}
		private bool _isSelected;

		public bool IsSelectedByKey => IsSelected && IsByKey;

		#endregion

		public bool IsTarget
		{
			get => _isTarget;
			set => SetPropertyValue(ref _isTarget, value);
		}
		private bool _isTarget;

		#region IDisposable

		private bool _isDisposed = false;

		protected override void Dispose(bool disposing)
		{
			lock (_lock)
			{
				if (_isDisposed)
					return;

				if (disposing)
				{
					_monitor.Dispose();
				}

				_isDisposed = true;

				base.Dispose(disposing);
			}
		}

		#endregion
	}
}