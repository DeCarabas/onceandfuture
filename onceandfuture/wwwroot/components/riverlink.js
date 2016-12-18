import React from 'react';
import { DEFAULT_LINK_COLOR } from './style';

const RiverLink = ({href, children}) => {
  const style = {
    color: DEFAULT_LINK_COLOR,
    textDecoration: 'initial',
  };
  return <a style={style} href={href} target="_blank">{children}</a>;
};

export default RiverLink;
