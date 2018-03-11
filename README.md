<p align="center"> 
  <img src="https://i.imgur.com/PoXC5AA.png" alt="alt logo">
</p>

[![GitHub release](https://img.shields.io/github/release/nxrighthere/BenchmarkNet.svg)](https://github.com/nxrighthere/BenchmarkNet/releases) [![PayPal](https://drive.google.com/uc?id=1OQrtNBVJehNVxgPf6T6yX1wIysz1ElLR)](https://www.paypal.me/nxrighthere) [![Bountysource](https://drive.google.com/uc?id=19QRobscL8Ir2RL489IbVjcw3fULfWS_Q)](https://salt.bountysource.com/checkout/amount?team=nxrighthere)

BenchmarkNet is a console application for testing the reliable UDP networking solutions.

Features:
- Asynchronous simulation of a large number of clients
- Stable under high loads
- Simple and flexible simulation setup
- Detailed session information

Supported networking libraries:
- [ENet](https://github.com/lsalzman/enet "ENet") ([C# Wrapper](https://github.com/NateShoffner/ENetSharp "C# Wrapper"))
- [UNet](https://docs.unity3d.com/Manual/UNetUsingTransport.html "UNet")
- [LiteNetLib](https://github.com/RevenantX/LiteNetLib "LiteNetLib")
- [Lidgren](https://github.com/lidgren/lidgren-network-gen3 "Lidgren")
- [MiniUDP](https://github.com/ashoulson/MiniUDP "MiniUDP")
- [Hazel](https://github.com/DarkRiftNetworking/Hazel-Networking "Hazel")
- [Photon](https://www.photonengine.com/en/OnPremise "Photon")
- [Neutrino](https://github.com/Claytonious/Neutrino)
- [DarkRift](https://darkriftnetworking.com/DarkRift2)

You can find the latest benchmark results on the [wiki page](https://github.com/nxrighthere/BenchmarkNet/wiki/Benchmark-Results).

How it works?
--------
Each simulated client is one asynchronous task for establishing a connection with the server and processing network events. Each task has one subtask which also works asynchronously to send network messages at a specified interval (15 messages per second by default). So, 1000 simulated clients is 1000 tasks with 1000 subtasks which works independently of each other. This sounds scary, but CPU usage is <1% for tasks itself and every operation is completely thread-safe. The clients send network messages to the server (500 reliable and 1000 unreliable by default). The server also sends messages to the clients in response (48 bytes per message by default). The application will monitor how the data is processed by the server and clients, and report their status in real-time.

Usage
--------
Run the application and enter the desired parameters to override the default values. Do not perform any actions while the benchmark is running and wait until the process is complete.

When you are going to perform a test with less than 256 simulated clients, it's highly recommended to switch [GC mode](https://github.com/nxrighthere/BenchmarkNet/wiki/Advanced-Options#gc-mode) from Server GC to Workstation GC.

You can use any packet sniffer to monitor how the data is transmitted, but it may affect the results.

If you want to simulate a bad network condition, use [Clumsy](http://jagt.github.io/clumsy/ "Clumsy") as an ideal companion.

Discussion
--------
Feel free to join the discussion in the [thread](https://forum.unity.com/threads/benchmarknet-stress-test-for-enet-unet-litenetlib-lidgren-and-miniudp.512507 "thread") on Unity forums.

If you have any questions, contact me via [email](mailto:nxrighthere@gmail.com "email").

Donations
--------
This project has already had an impact and helped developers in an improvement of their networking libraries. If you like this project, you can support me on [PayPal](https://www.paypal.me/nxrighthere)
or [Bountysource](https://salt.bountysource.com/checkout/amount?team=nxrighthere).

Bitcoin `173ZRNuT4k7ss7rAVmNCZr6hLfFY4DZubU` Litecoin `LhdxsS7AUhUrKMmMyTX1yL4EQp6tzSonDT`

Any support is much appreciated.

Supporters
--------
<p align="left"> 
  <img src="https://drive.google.com/uc?id=1jVxLviw_CHnjygdfSIdGzLQS2DBdYsa8" alt="supporters">
</p>
