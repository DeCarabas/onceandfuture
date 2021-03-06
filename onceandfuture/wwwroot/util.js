// @flow
// @format
export function assert(condition /*:boolean*/, message /*:string*/) {
  if (!condition) {
    /* eslint-disable no-debugger, no-console */
    debugger;
    console.log("Assertion failed: ", message);
    /* eslint-enable */
    throw Error("Assertion failed: " + message);
  }
}

export function update_key(
  update /*:{feedUrl: string, whenLastUpdate:string}*/
) {
  return update.feedUrl + "|" + update.whenLastUpdate;
}

export function make_full_url(url /*:string*/) {
  let full_url = url;
  // if (full_url.startsWith('http')) { return full_url; }
  // if (!full_url.startsWith('/')) {
  //   full_url = '/' + full_url;
  // }
  // if (!full_url.startsWith('http')) {
  //   full_url = 'http://localhost:5000' + full_url;
  // }
  return full_url;
}
