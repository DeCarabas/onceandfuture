export function assert(condition, message) {
  if (!condition) {
    debugger;
    console.log('Assertion failed: ', message);
    throw Error('Assertion failed', message);
  }
}

export function update_key(update) {
  return update.feedUrl + '|' + update.whenLastUpdate;
}

export function make_full_url(url) {
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
