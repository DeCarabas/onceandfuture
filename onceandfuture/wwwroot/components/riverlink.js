var React = require('react'); // N.B. Still need this because JSX.
import { DEFAULT_LINK_STYLE } from './style'
// import { shell } from 'electron';

function handleLinkClick(evt, link) {
  let open_background = false;
  if (evt.metaKey || evt.ctrlKey) {
    open_background = true;
  }

  shell.openExternal(link, {activate: !open_background});
  evt.preventDefault();
  return true;
}

const RiverLink = ({href, children}) => {
  return (
    <a
      style={DEFAULT_LINK_STYLE}
      href={href}
      onClick={(evt) => handleLinkClick(evt, href)}
      target="_blank"
    >
      {children}
    </a>
  );
}

export default RiverLink
