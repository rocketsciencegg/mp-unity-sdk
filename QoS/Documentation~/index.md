# QoS SDK

The QoS SDK provides connectivity Quality of Service (QoS) measurements from Unity clients to Multiplay
services running in multiple geographic regions.

Using this SDK in conjunction with [Multiplay hosting](https://docs.multiplay.dev/index/welcome-to-multiplay) can take into account a client's latency
and location when determining the best server to connect to.

## Supported versions

The following Unity Editor versions support the QoS SDK:

* 6000.3.2f1+
* 2022.2.0a10+
* 2022.1.0f1+
* 2021.3.2f1+
* 2020.3.34f1+

Invoking the SDK in unsupported Unity versions will throw an exception.

## Using the QoS SDK

### Import packages

```c#
using RocketScience.Services.Qos;
```

### Run QoS measurements

You can obtain a list of QoS results sorted by latency and packet loss through a single call to the QoS SDK. Specify the Unity
service to get a list of QoS measurements for all regions available for that service.

The following code sample demonstrates how to use the `GetSortedQosResultsAsync` method.

```c#
try
{
    var serviceName = "multiplay";
    var qosResults = await QosService.Instance.GetSortedQosResultsAsync(serviceName, null);
}
catch (Exception e)
{
    Debug.Log(e);
}
```

If you need a more refined list of regions, obtain that list of regions before passing it to the QoS SDK. The SDK only returns
QoS measurements for the specified regions in such a case.

The following code sample demonstrates how to get measurements for a refined set of regions. Obtaining the list of regions is
outside the scope of the QoS SDK.

```c#
try
{
    var regions = new List<string>();
    // Populate `regions` with a subset of multiplay regions (outside of the scope of this SDK)

    var serviceName = "multiplay";
    var qosResults = await QosService.Instance.GetSortedQosResultsAsync(serviceName, regions);
}
catch (Exception e)
{
    Debug.Log(e);
}
```

## SDK Documentation
The SDK is documented using XML comments. These will work with most IDEs to provide IntelliSense and inline documentation.
