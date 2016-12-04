var React = require('react'); // N.B. Still need this because JSX.
import { DEFAULT_LINK_STYLE } from './style';

const RiverLink = ({href, children}) => {
  return (
    <a style={DEFAULT_LINK_STYLE} href={href} target="_blank">
      {children}
    </a>
  );
};

export default RiverLink;
