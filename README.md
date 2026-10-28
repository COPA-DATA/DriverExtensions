# DriverExtensions
A repository to get started with zenon .NET drivers. By using .NET drivers you are able to create connectivity to third party systems. This repository contains samples how to connect to REST Web Service based or MQTT based apps. 

zenon is a software system from COPA-DATA for industrial automation and the energy industry. Machines and equipment are controlled, monitored and optimized. zenon's particular strength is open and reliable communication throughout heterogeneous production facilities. Over 300 native communication protocols support the horizontal and vertical exchange of data. This allows for the continuous implementation of Industrial IoT and the Smart Factory.

zenon’s engineering environment is flexible and can be used in many ways. Complex functions for comprehensive applications such as HMI/SCADA and reporting are supplied out of the box to create intuitive and robust applications. Users can thus contribute to the increased flexibility and efficiency of HMI applications using zenon.

See Wiki for [getting started](../../wiki).

## Building and running

The driver samples in `Samples/` reference `CopaData.Drivers.Contracts.dll` from a zenon 16 installation, which is found through the registry. If zenon is installed elsewhere, or not registered on the build machine, create a `Directory.Build.local.props` in the repository root (it is not committed):

```xml
<Project>
  <PropertyGroup>
    <ZenonProgramDir>C:\Path\To\zenon Software Platform 16\Engineering\</ZenonProgramDir>
  </PropertyGroup>
</Project>
```

After a build, each sample is copied to `<zenon>\DriverExtensions\BasicSample`, `MqttSample` or `RestSample`. Writing to Program Files requires administrative rights; if the copy fails, the build only shows a warning. Build with `/p:DeployToZenon=false` to skip the copy.

The MQTT and REST samples read their settings from the configuration file selected in the zenon driver configuration: `mqttsampleconfig.json` (broker address, client ID) and `restsampleconfig.json` (web service address).

The simulators in `Simulation/` provide test data for the samples:

- **RestSimulatorService** serves sensor values on `http://localhost:5000` (the address used by the REST sample by default).
- **MqttSimulatorClient** publishes sensor values to an MQTT broker using MQTT 3.1.1. The broker address and the number of sensors are set in its `appsettings.json`.
