FROM php:8.5-apache@sha256:974e3a920309308e1690ed607e6cc3061d6906f90cc8d30fe519daa48bccf101
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80