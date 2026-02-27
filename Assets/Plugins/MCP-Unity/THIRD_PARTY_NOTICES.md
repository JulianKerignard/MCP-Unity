# Third-Party Notices

MCP Unity — AI Editor Assistant includes the following third-party software components.

---

## websocket-sharp

**Location**: `Plugins/websocket-sharp.dll`  
**Project**: https://github.com/sta/websocket-sharp  
**Author**: sta  
**License**: MIT

```
The MIT License (MIT)

Copyright (c) 2010-2023 sta

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## @modelcontextprotocol/sdk

**Location**: `Server~/node_modules/@modelcontextprotocol/` (Node.js bridge, not imported by Unity)  
**Project**: https://github.com/modelcontextprotocol/typescript-sdk  
**License**: MIT

---

## Additional Node.js Dependencies (Server~ bridge only)

The `Server~/` directory contains a Node.js bridge that is not imported by Unity.
It uses the following open-source packages, all under MIT or Apache-2.0 licenses:

- `ws` — MIT — https://github.com/websockets/ws
- `zod` — MIT — https://github.com/colinhacks/zod
- `tsx` — MIT — https://github.com/privatenumber/tsx
- `vitest` — MIT — https://github.com/vitest-dev/vitest
- `typescript` — Apache-2.0 — https://github.com/microsoft/TypeScript

Full license texts for Node.js dependencies are available in their respective
`node_modules/*/LICENSE` files when the bridge is built locally via `npm install`.
