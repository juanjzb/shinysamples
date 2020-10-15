﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Prism.Navigation;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Samples.Infrastructure;
using Shiny;
using Shiny.BluetoothLE;


namespace Samples.BluetoothLE
{
    public class AdapterViewModel : ViewModel
    {
        public AdapterViewModel(INavigationService navigator,
                                IDialogs dialogs,
                                IBleManager? bleManager = null)
        {
            this.IsScanning = bleManager?.IsScanning ?? false;
            this.CanControlAdapterState = bleManager?.CanControlAdapterState() ?? false;

            this.WhenAnyValue(x => x.SelectedPeripheral)
                .Skip(1)
                .Where(x => x != null)
                .Subscribe(async x => await navigator.Navigate("Peripheral", ("Peripheral", x.Peripheral)));

            this.ToggleAdapterState = ReactiveCommand.CreateFromTask(
                async () =>
                {
                    if (bleManager == null)
                    {
                        await dialogs.Alert("Platform Not Supported");
                    }
                    else
                    {
                        var poweredOn = bleManager.Status == AccessState.Available;
                        if (!bleManager.TrySetAdapterState(!poweredOn))
                            await dialogs.Alert("Cannot change bluetooth adapter state");
                    }
                }
            );

            this.ScanToggle = ReactiveCommand.CreateFromTask(
                async () =>
                {
                    if (bleManager == null)
                    {
                        await dialogs.Alert("Platform Not Supported");
                        return;
                    }
                    if (this.IsScanning)
                    {
                        this.Deactivate();
                    }
                    else
                    {
                        this.IsScanning = true;
                        this.Peripherals.Clear();
                        this.RaisePropertyChanged(nameof(this.Peripherals));

                        bleManager
                            .Scan()
                            .Buffer(TimeSpan.FromSeconds(1))
                            .SubOnMainThread(
                                results =>
                                {
                                    var list = new List<PeripheralItemViewModel>();
                                    foreach (var result in results)
                                    {
                                        var peripheral = this.Peripherals.FirstOrDefault(x => x.Equals(result.Peripheral));
                                        if (peripheral == null)
                                            peripheral = list.FirstOrDefault(x => x.Equals(result.Peripheral));

                                        if (peripheral != null)
                                        {
                                            peripheral.Update(result);
                                        }
                                        else
                                        {
                                            peripheral = new PeripheralItemViewModel(result.Peripheral);
                                            peripheral.Update(result);
                                            list.Add(peripheral);
                                        }
                                    }
                                    if (list.Any())
                                    {
                                        list.AddRange(this.Peripherals);
                                        this.Peripherals = list;
                                    }
                                },
                                ex => dialogs.Alert(ex.ToString(), "ERROR")
                            )
                            .DisposeWith(this.DeactivateWith);
                    }
                }
            );
        }


        public ICommand ScanToggle { get; }
        public ICommand ToggleAdapterState { get; }
        public bool CanControlAdapterState { get; }
        [Reactive] public List<PeripheralItemViewModel> Peripherals { get; private set; }
        [Reactive] public PeripheralItemViewModel SelectedPeripheral { get; set; }
        [Reactive] public bool IsScanning { get; private set; }
    }
}