import React from 'react';
import { SIZE_FULL_IMAGE_WIDTH } from './style';
import { make_full_url } from '../util';
import RiverLink from './riverlink';

const RiverItemThumbnail = ({item, mode = 'auto'}) => {
  let thumb = item.thumbnail;
  if (thumb) {
    let imgstyle = {
      width: 100,
      height: 100,
      marginTop: 10,
      marginLeft: 3,
      marginRight: 3,
      marginBottom: 3,
    };

    if (mode === 'auto') {
      if ((thumb.width < SIZE_FULL_IMAGE_WIDTH) || (item.body || '').length > 100) {
        mode = 'text';
      } else {
        mode = 'image';
      }
    }

    if (mode === 'text') {
      imgstyle.float = 'right';
      imgstyle.width = 100;
      imgstyle.height = 100;
    } else {
      imgstyle.width = SIZE_FULL_IMAGE_WIDTH;
      imgstyle.height = SIZE_FULL_IMAGE_WIDTH;
    }

    return (
      <RiverLink href={item.link}>
        <img style={imgstyle} src={make_full_url(thumb.url)} />
      </RiverLink>
    );
  } else {
    return <span />;
  }
}

export default RiverItemThumbnail;
