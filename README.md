<!-- SHIELDS -->
<div align="center">

[![Contributors][contributors-shield]][contributors-url]
[![Forks][forks-shield]][forks-url]
[![Stargazers][stars-shield]][stars-url]
[![Issues][issues-shield]][issues-url]
[![MIT License][license-shield]][license-url]

</div>

<!-- PROJECT LOGO -->
<br />
<div align="center">
  <h1 align="center">serve</h1>
  <p align="center">
    A simple C# static web content file server.
    <br />
    <a href="https://github.com/thgossler/serve/issues">Report Bug</a>
    ·
    <a href="https://github.com/thgossler/serve/issues">Request Feature</a>
    ·
    <a href="https://github.com/thgossler/serve#contributing">Contribute</a>
    ·
    <a href="https://github.com/sponsors/thgossler">Sponsor project</a>
    ·
    <a href="https://www.paypal.com/donate/?hosted_button_id=JVG7PFJ8DMW7J">Sponsor via PayPal</a>
  </p>
</div>

## Overview

This project implements a simple .NET 8 C# based single-file console application which serves static web content 
from a local filesystem folder on localhost. It supports HTTPS and handles the creation and use of the self-signed
server certificate automatically.

Command line syntax:

```shell
serve [<rootFolder>] [--port:<number>] [--https] [--exitTimeoutSecs:<seconds>]
    --port             Port number to listen on (default: 8080)
    --https            Use HTTPS (default: false)
    --exitTimeoutSecs  Timeout in seconds to wait for requests before exiting (default: 300)
```

Example:

```shell
serve --port:3000 --https
```

This will check the availability of the self-signed server certificate, eventually create and install it, and
then serve the contents of the current directory under `https://localhost:3000/`.

## Contributing

Contributions are what make the open source community such an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

If you have a suggestion that would make this better, please fork the repo and create a pull request. You can also simply open an issue with the tag "enhancement".
Don't forget to give the project a star :wink: Thanks!

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## Donate

If you are using the tool but are unable to contribute technically, please consider promoting it and donating an amount that reflects its value to you. You can do so either via PayPal

[![Donate via PayPal](https://www.paypalobjects.com/en_US/i/btn/btn_donate_LG.gif)](https://www.paypal.com/donate/?hosted_button_id=JVG7PFJ8DMW7J)

or via [GitHub Sponsors](https://github.com/sponsors/thgossler).

## License

Distributed under the MIT License. See [`LICENSE`](https://github.com/thgossler/serve/blob/main/LICENSE) for more information.

<!-- MARKDOWN LINKS & IMAGES (https://www.markdownguide.org/basic-syntax/#reference-style-links) -->
[contributors-shield]: https://img.shields.io/github/contributors/thgossler/serve.svg
[contributors-url]: https://github.com/thgossler/serve/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/thgossler/serve.svg
[forks-url]: https://github.com/thgossler/serve/network/members
[stars-shield]: https://img.shields.io/github/stars/thgossler/serve.svg
[stars-url]: https://github.com/thgossler/serve/stargazers
[issues-shield]: https://img.shields.io/github/issues/thgossler/serve.svg
[issues-url]: https://github.com/thgossler/serve/issues
[license-shield]: https://img.shields.io/github/license/thgossler/serve.svg
[license-url]: https://github.com/thgossler/serve/blob/main/LICENSE
